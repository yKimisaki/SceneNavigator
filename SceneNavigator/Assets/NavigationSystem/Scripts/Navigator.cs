using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tonari.Unity.SceneNavigator
{
    public class Navigator
    {
        private ILogger _logger;

        private ConcurrentDictionary<string, INavigatableScene> _scenesByName;
        private ConcurrentStack<NavigationStackElement> _navigateHistoryStack;
        private INavigatableScene _currentScene;

        private ILoadingDisplaySelector _loadingDisplaySelector;
        private ConcurrentDictionary<int, ILoadingDisplay> _loadingDisplaysByOption;

        private ConcurrentDictionary<Guid, TaskCompletionSource<object>> _taskCompletionSourcesByResultRequirementId;

        private ICanvasOrderArranger _canvasOrderArranger;
        
        public event Func<INavigatableScene, INavigatableScene, TransitionMode, Task> OnNavigatedAsync;

        private ICanvasCustomizer _canvasCustomizer;

        public Navigator(ILogger logger, ILoadingDisplaySelector loadingDisplaySelector, ICanvasCustomizer canvasCustomizer, ICanvasOrderArranger canvasOrderArranger)
        {
            this._logger = logger;

            this._scenesByName = new ConcurrentDictionary<string, INavigatableScene>();

            this._navigateHistoryStack = new ConcurrentStack<NavigationStackElement>();

            this._loadingDisplaySelector = loadingDisplaySelector;
            this._loadingDisplaysByOption = new ConcurrentDictionary<int, ILoadingDisplay>();

            this._taskCompletionSourcesByResultRequirementId = new ConcurrentDictionary<Guid, TaskCompletionSource<object>>();

            this._canvasOrderArranger = canvasOrderArranger ?? new DefaultCanvasOrderArranger();
            
            this._canvasCustomizer = canvasCustomizer;
        }

        public virtual Task NavigateAsync(SceneArgs args, IProgress<float> progress = null)
        {
            // 結果を待ってるシーンがあるならダメ
            if (this._taskCompletionSourcesByResultRequirementId.Count > 0)
            {
                this._logger.LogException(new NavigationFailureException("結果を待っているシーンがあります"));
                return Task.CompletedTask;
            }

            return NavigateCoreAsync(args, NavigationOption.Push, progress);
        }

        public virtual async Task NavigateBackAsync(object result = null, IProgress<float> progress = null)
        {
            var previousObject = default(NavigationStackElement);
            var previousScene = default(INavigatableScene);

            if (!this._navigateHistoryStack.TryPeek(out previousObject))
            {
                this._logger.LogException(new NavigationFailureException("シーンスタックがありません"));
                return;
            }

            if (!this._scenesByName.TryGetValue(previousObject.SceneName, out previousScene))
            {
                this._logger.LogException(new NavigationFailureException("無効なシーンが設定されています"));
                return;
            }

            // 先に結果要求IDを貰っておく
            var resultRequirementId = previousScene.ResultRequirementId;

            // 遷移
            var option = NavigationOption.Pop;
            if (previousObject.TransitionMode.HasFlag(TransitionMode.KeepCurrent))
            {
                option |= NavigationOption.Override;
            }
            await NavigateCoreAsync(previousScene.ParentSceneArgs, option, progress);

            if (resultRequirementId.HasValue)
            {
                var taskCompletionSource = default(TaskCompletionSource<object>);
                if (!_taskCompletionSourcesByResultRequirementId.TryRemove(resultRequirementId.Value, out taskCompletionSource))
                {
                    this._logger.LogException(new NavigationFailureException("戻り値が取得できません"));
                    return;
                }

                taskCompletionSource.SetResult(result);
            }
        }

        public virtual async Task<TResult> NavigateAsPopupAsync<TResult>(SceneArgs args, IProgress<float> progress = null)
        {
            var resultRequirementId = Guid.NewGuid();
            var taskCompletionSource = new TaskCompletionSource<object>();

            if (!this._taskCompletionSourcesByResultRequirementId.TryAdd(resultRequirementId, taskCompletionSource))
            {
                this._logger.LogException(new NavigationFailureException("シーンをテーブルに追加できませんでした"));
                return default(TResult);
            }

            var activateResult = await NavigateCoreAsync(args, NavigationOption.Popup, progress);
            // ここでダメな場合は既にActivateAsyncでエラーを吐いてるハズ
            if (activateResult == null)
            {
                return default(TResult);
            }

            activateResult.ActivatedScene.ResultRequirementId = resultRequirementId;

            var result = await taskCompletionSource.Task;

            if (!(result is TResult))
            {
                this._logger.LogException(new NavigationFailureException($"戻り値の型は{typeof(TResult)}を期待しましたが、{result.GetType()}が返されました"));
                return default(TResult);
            }

            return (TResult)result;
        }

        private async Task<ActivationResult> NavigateCoreAsync(SceneArgs args, NavigationOption option = NavigationOption.None, IProgress<float> progress = null)
        {
            var loadingDisplay = this._loadingDisplaySelector != null ? this._loadingDisplaysByOption.GetOrAdd((int)option, this._loadingDisplaySelector.SelectDisplay(option)) : null;

            if (loadingDisplay != null)
            {
                loadingDisplay.Show();
            }

            ActivationResult activationResult;
            if (this._scenesByName.ContainsKey(args.SceneName))
            {
                // 既にInitialize済みのSceneであればActivateするだけでOK
                activationResult = Activate(args, option);
            }
            else
            {
                activationResult = await LoadAsync(args, option, progress);
            }
            // ここでダメな場合は既にActivateAsyncでエラーを吐いてるハズ
            if (activationResult == null || activationResult.ActivatedScene == null)
            {
                return null;
            }

            if (option.HasFlag(NavigationOption.Push))
            {
                // 新しいシーンをスタックに積む
                this._navigateHistoryStack.Push(new NavigationStackElement() { SceneName = args.SceneName, TransitionMode = activationResult.TransitionMode });
            }

            // 新しいシーンをリセットする
            await activationResult.ActivatedScene.ResetAsync(args, activationResult.TransitionMode);

            // 新規シーンなら初期化する
            if (activationResult.TransitionMode.HasFlag(TransitionMode.New))
            {
                activationResult.ActivatedScene.Initialize();
            }

            // 新規シーンに入る
            activationResult.ActivatedScene.RootObject.SetActive(true);
            await activationResult.ActivatedScene.EnterAsync(activationResult.TransitionMode);

            // 入ったらイベント発火
            if (this.OnNavigatedAsync != null)
            {
                await this.OnNavigatedAsync(activationResult.ActivatedScene, activationResult.DisactivatedScene, activationResult.TransitionMode);
            }
            
            // 古いシーンから出る
            if (activationResult.DisactivatedScene != null)
            {
                await activationResult.DisactivatedScene.LeaveAsync(activationResult.TransitionMode);

                // 上に乗せるフラグが無ければ非アクティブ化
                if (!option.HasFlag(NavigationOption.Override))
                {
                    activationResult.DisactivatedScene.RootObject.SetActive(false);
                }

                // Popするならアンロードも行う
                if (option.HasFlag(NavigationOption.Pop))
                {
                    // 古いシーンをスタックから抜いてアンロード
                    var popObject = default(NavigationStackElement);
                    this._navigateHistoryStack.TryPop(out popObject);
                    await UnloadAsync(activationResult.DisactivatedScene.SceneArgs, progress);
                }
            }

            if (loadingDisplay != null)
            {
                loadingDisplay.Hide();
            }

            return activationResult;
        }

        private async Task<ActivationResult> LoadAsync(SceneArgs args, NavigationOption option = NavigationOption.None, IProgress<float> progress = null)
        {
            var asyncOperation = SceneManager.LoadSceneAsync(args.SceneName, LoadSceneMode.Additive);

            progress?.Report(0f);
            while (!asyncOperation.isDone)
            {
                progress?.Report(asyncOperation.progress);
                await Task.Delay(TimeSpan.FromSeconds(Time.fixedDeltaTime));
            }
            progress?.Report(1f);
            
            var result = Activate(args, option);

            // ここに来たという事は新規
            result.TransitionMode |= TransitionMode.New;

            if (!this._scenesByName.TryAdd(args.SceneName, result.ActivatedScene))
            {
                this._logger.LogException(new NavigationFailureException("シーンをテーブルに追加できませんでした"));
                return null;
            }

            // ロード時にCanvasの調整をする
            if (this._canvasCustomizer != null)
            {
                this._canvasCustomizer.Customize(result.ActivatedScene.RootCanvas);
            }

            return result;
        }

        private async Task UnloadAsync(SceneArgs args, IProgress<float> progress = null)
        {
            var asyncOperation = SceneManager.UnloadSceneAsync(args.SceneName);

            progress?.Report(0f);
            while (!asyncOperation.isDone)
            {
                progress?.Report(asyncOperation.progress);
                await Task.Delay(TimeSpan.FromSeconds(Time.fixedDeltaTime));
            }
            progress?.Report(1f);

            var removedScene = default(INavigatableScene);
            if (!this._scenesByName.TryRemove(args.SceneName, out removedScene))
            {
                this._logger.LogException(new NavigationFailureException("シーンをテーブルから削除できませんでした"));
            }
        }

        private ActivationResult Activate(SceneArgs args, NavigationOption option = NavigationOption.None)
        {
            var result = new ActivationResult();

            if (this._currentScene != null)
            {
                var currentUnityScene = SceneManager.GetSceneByName(this._currentScene.SceneArgs.SceneName);
                if (!currentUnityScene.isLoaded)
                {
                    this._logger.LogException(new NavigationFailureException("無効なシーンが設定されています"));
                    return null;
                }

                result.DisactivatedScene = this._currentScene;
            }
            
            // シーンマネージャの方から次のSceneを取得
            var nextUnityScene = SceneManager.GetSceneByName(args.SceneName);
            if (!nextUnityScene.isLoaded)
            {
                this._logger.LogException(new NavigationFailureException("シーンの読み込みに失敗しました"));
                return null;
            }
            if (nextUnityScene.rootCount != 1)
            {
                this._logger.LogException(new NavigationFailureException("シーンのRootObjectが複数あります"));
                return null;
            }

            // SceneからINavigatableSceneを取得
            var rootObjects = nextUnityScene.GetRootGameObjects();
            if (rootObjects.Length == 0)
            {
                this._logger.LogException(new NavigationFailureException("RootObjectが存在しません"));
            }
            if (rootObjects.Length > 1)
            {
                this._logger.LogException(new NavigationFailureException("RootObjectが複数あります"));
            }

            var containsCanvases = rootObjects[0].GetComponentsInChildren<Canvas>();
            if (containsCanvases.Length == 0)
            {
                this._logger.LogException(new NavigationFailureException("Canvasが見つかりませんでした"));
                return null;
            }

            var sceneBases = rootObjects[0].GetComponents<SceneBase>();
            if (sceneBases.Length == 0)
            {
                this._logger.LogException(new NavigationFailureException("SceneBaseコンポーネントがRootObjectに存在しません"));
                return null;
            }
            if (sceneBases.Length > 1)
            {
                this._logger.LogException(new NavigationFailureException("SceneBaseコンポーネントが複数あります"));
                return null;
            }

            // 進む場合、新しいシーンは非表示にしておく
            if (!option.HasFlag(NavigationOption.Pop))
            {
                rootObjects[0].SetActive(false);
            }

            // 次のシーンに諸々引数を渡す
            var nextScene = sceneBases[0] as INavigatableScene;
            nextScene.SetRootCanvas(containsCanvases[0]);
            nextScene.SceneArgs = args;
            nextScene.SetNavigator(this);
            nextScene.SetLogger(this._logger);
            if (this._currentScene != null)
            {
                nextScene.SetParentSceneArgs(this._currentScene.SceneArgs);
            }

            // 進む場合、ソートを整える
            if (!option.HasFlag(NavigationOption.Pop))
            {
                if (this._currentScene != null)
                {
                    nextScene.RootCanvas.sortingOrder = this._canvasOrderArranger.GetOrder(this._currentScene.RootCanvas.sortingOrder, option);
                }
                else
                {
                    nextScene.RootCanvas.sortingOrder = this._canvasOrderArranger.InitialOrder;
                }
            }

            // 次のシーンにnextSceneを設定
            this._currentScene = result.ActivatedScene = nextScene;

            // TransitionModeの調整
            if (option.HasFlag(NavigationOption.Override))
            {
                result.TransitionMode |= TransitionMode.KeepCurrent;
            }
            if (option.HasFlag(NavigationOption.Pop))
            {
                result.TransitionMode |= TransitionMode.Back;
            }

            return result;
        }

        private class ActivationResult
        {
            public INavigatableScene ActivatedScene { get; set; }
            public INavigatableScene DisactivatedScene { get; set; }

            public TransitionMode TransitionMode { get; set; }
        }

        private class NavigationStackElement
        {
            public string SceneName { get; set; }
            public TransitionMode TransitionMode { get; set; }
        }
    }
}