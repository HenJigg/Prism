﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Prism.Common;
using Prism.Ioc;

namespace Prism.Services.Dialogs
{
    /// <summary>
    /// Implements <see cref="IDialogService"/> to show modal and non-modal dialogs.
    /// </summary>
    /// <remarks>
    /// The dialog's ViewModel must implement IDialogAware.
    /// </remarks>
    public class DialogService : IDialogService
    {
        private readonly IContainerExtension _containerExtension;

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogService"/> class.
        /// </summary>
        /// <param name="containerExtension"></param>
        public DialogService(IContainerExtension containerExtension)
        {
            _containerExtension = containerExtension;
        }

        /// <summary>
        /// Shows a non-modal dialog.
        /// </summary>
        /// <param name="name">The name of the dialog to show.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        public void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback)
        {
            _ = ShowDialogInternal(name, parameters, callback, false);
        }

        /// <summary>
        /// Shows a non-modal dialog.
        /// </summary>
        /// <param name="name">The name of the dialog to show.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        /// <param name="windowName">The name of the hosting window registered with the IContainerRegistry.</param>
        public void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback, string windowName)
        {
            _ = ShowDialogInternal(name, parameters, callback, false, windowName);
        }

        /// <summary>
        /// Shows a modal dialog.
        /// </summary>
        /// <param name="name">The name of the dialog to show.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        public void ShowDialog(string name, IDialogParameters parameters, Action<IDialogResult> callback)
        {
            _ = ShowDialogInternal(name, parameters, callback, true);
        }

        /// <summary>
        /// Async Shows a modal dialog.
        /// </summary>
        /// <param name="name">The name of the dialog to show.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        public async Task ShowDialogAsync(string name, IDialogParameters parameters, Action<IDialogResult> callback)
        {
            await ShowDialogInternal(name, parameters, callback, true);
        }

        /// <summary>
        /// Shows a modal dialog.
        /// </summary>
        /// <param name="name">The name of the dialog to show.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        /// <param name="windowName">The name of the hosting window registered with the IContainerRegistry.</param>
        public void ShowDialog(string name, IDialogParameters parameters, Action<IDialogResult> callback, string windowName)
        {
            _ = ShowDialogInternal(name, parameters, callback, true, windowName);
        }

        async Task ShowDialogInternal(string name, IDialogParameters parameters, Action<IDialogResult> callback, bool isModal, string windowName = null)
        {
            if (parameters == null)
                parameters = new DialogParameters();

            IDialogWindow dialogWindow = CreateDialogWindow(windowName);
            ConfigureDialogWindowEvents(dialogWindow, callback);
            await ConfigureDialogWindowContent(name, dialogWindow, parameters);
            ShowDialogWindow(dialogWindow, isModal);
        }

        /// <summary>
        /// Shows the dialog window.
        /// </summary>
        /// <param name="dialogWindow">The dialog window to show.</param>
        /// <param name="isModal">If true; dialog is shown as a modal</param>
        protected virtual void ShowDialogWindow(IDialogWindow dialogWindow, bool isModal)
        {
            if (isModal)
                dialogWindow.ShowDialog();
            else
                dialogWindow.Show();
        }

        /// <summary>
        /// Create a new <see cref="IDialogWindow"/>.
        /// </summary>
        /// <param name="name">The name of the hosting window registered with the IContainerRegistry.</param>
        /// <returns>The created <see cref="IDialogWindow"/>.</returns>
        protected virtual IDialogWindow CreateDialogWindow(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return _containerExtension.Resolve<IDialogWindow>();
            else
                return _containerExtension.Resolve<IDialogWindow>(name);
        }

        /// <summary>
        /// Configure <see cref="IDialogWindow"/> content.
        /// </summary>
        /// <param name="dialogName">The name of the dialog to show.</param>
        /// <param name="window">The hosting window.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        protected virtual async Task ConfigureDialogWindowContent(string dialogName, IDialogWindow window, IDialogParameters parameters)
        {
            var content = _containerExtension.Resolve<object>(dialogName);
            if (!(content is FrameworkElement dialogContent))
                throw new NullReferenceException("A dialog's content must be a FrameworkElement");

            MvvmHelpers.AutowireViewModel(dialogContent);

            if (!(dialogContent.DataContext is IDialogAware viewModel))
                throw new NullReferenceException("A dialog's ViewModel must implement the IDialogAware interface");

            if (!(dialogContent.DataContext is IDialogAsyncAware asyncviewModel))
                throw new NullReferenceException("A dialog's ViewModel must implement the IDialogAsyncAware interface");

            if (viewModel != null)
            {
                ConfigureDialogWindowProperties(window, dialogContent, viewModel);
                viewModel.OnDialogOpened(parameters);
            }
            else if (asyncviewModel != null)
            {
                ConfigureDialogWindowProperties(window, dialogContent, asyncviewModel);
                await asyncviewModel.OnDialogOpenedAsync(parameters);
            }
        }

        /// <summary>
        /// Configure <see cref="IDialogWindow"/> and <see cref="IDialogAware"/> events.
        /// </summary>
        /// <param name="dialogWindow">The hosting window.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        protected virtual void ConfigureDialogWindowEvents(IDialogWindow dialogWindow, Action<IDialogResult> callback)
        {
            Action<IDialogResult> requestCloseHandler = null;
            requestCloseHandler = (o) =>
            {
                dialogWindow.Result = o;
                dialogWindow.Close();
            };

            RoutedEventHandler loadedHandler = null;
            loadedHandler = (o, e) =>
            {
                dialogWindow.Loaded -= loadedHandler;
                dialogWindow.GetDialogViewModel().RequestClose += requestCloseHandler;
            };
            dialogWindow.Loaded += loadedHandler;

            CancelEventHandler closingHandler = null;
            closingHandler = (o, e) =>
            {
                if (!dialogWindow.GetDialogViewModel().CanCloseDialog())
                    e.Cancel = true;
            };
            dialogWindow.Closing += closingHandler;

            EventHandler closedHandler = null;
            closedHandler = (o, e) =>
                {
                    dialogWindow.Closed -= closedHandler;
                    dialogWindow.Closing -= closingHandler;
                    dialogWindow.GetDialogViewModel().RequestClose -= requestCloseHandler;

                    dialogWindow.GetDialogViewModel().OnDialogClosed();

                    if (dialogWindow.Result == null)
                        dialogWindow.Result = new DialogResult();

                    callback?.Invoke(dialogWindow.Result);

                    dialogWindow.DataContext = null;
                    dialogWindow.Content = null;
                };
            dialogWindow.Closed += closedHandler;
        }

        /// <summary>
        /// Configure <see cref="IDialogWindow"/> properties.
        /// </summary>
        /// <param name="window">The hosting window.</param>
        /// <param name="dialogContent">The dialog to show.</param>
        /// <param name="viewModel">The dialog's ViewModel.</param>
        protected virtual void ConfigureDialogWindowProperties<T>(IDialogWindow window, FrameworkElement dialogContent, T viewModel)
        {
            var windowStyle = Dialog.GetWindowStyle(dialogContent);
            if (windowStyle != null)
                window.Style = windowStyle;

            window.Content = dialogContent;

            window.DataContext = viewModel; //we want the host window and the dialog to share the same data context

            if (window.Owner == null)
                window.Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);
        }
    }
}
