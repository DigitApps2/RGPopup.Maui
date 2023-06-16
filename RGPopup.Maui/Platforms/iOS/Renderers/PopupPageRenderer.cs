﻿
using System.Diagnostics;
using CoreGraphics;

using Foundation;
using Microsoft.Maui.Controls.Compatibility.Platform.iOS;
using RGPopup.Maui.IOS.Extensions;
using RGPopup.Maui.Pages;

using UIKit;

namespace RGPopup.Maui.IOS.Renderers
{
    public class PopupPageRenderer : UIViewController
    {
        private readonly PopupPageHandler _pageHandler;
        private readonly UIGestureRecognizer _tapGestureRecognizer;
        private NSObject? _willChangeFrameNotificationObserver;
        private NSObject? _willHideNotificationObserver;
        private bool _isDisposed;

        public PopupPageHandler? Handler => _pageHandler;
        internal CGRect KeyboardBounds { get; private set; } = CGRect.Empty;
        internal PopupPage? CurrentElement => (PopupPage)(Handler?.VirtualView);
        
        #region Main Methods

        public PopupPageRenderer(PopupPageHandler pageHandler)
        {
            _pageHandler = pageHandler;
            
            _tapGestureRecognizer = new UITapGestureRecognizer(OnTap)
            {
                CancelsTouchesInView = false
            };
        }
        
        public PopupPageRenderer(IntPtr handle) : base(handle)
        {
            // Fix #307
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                View?.RemoveGestureRecognizer(_tapGestureRecognizer);
            }

            base.Dispose(disposing);

            _isDisposed = true;
        }

        #endregion

        #region Gestures Methods

        private void OnTap(UITapGestureRecognizer e)
        {
            if (CurrentElement == null) return;
            
            var view = e.View;
            var location = e.LocationInView(view);
            var subview = view.HitTest(location, null);
            if (Equals(subview, view))
            {
                _ = CurrentElement.SendBackgroundClick();
            }
        }

        #endregion

        #region Life Cycle Methods

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            ModalPresentationStyle = UIModalPresentationStyle.OverCurrentContext;
            ModalTransitionStyle = UIModalTransitionStyle.CoverVertical;

            View?.AddGestureRecognizer(_tapGestureRecognizer);
        }

        public override void ViewDidUnload()
        {
            base.ViewDidUnload();

            View?.RemoveGestureRecognizer(_tapGestureRecognizer);
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            UnregisterAllObservers();

            _willChangeFrameNotificationObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillShowNotification, KeyBoardUpNotification);
            _willHideNotificationObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillHideNotification, KeyBoardDownNotification);
        }

        public override void ViewWillDisappear(bool animated)
        {
            base.ViewWillDisappear(animated);

            UnregisterAllObservers();
        }

        #endregion

        #region Layout Methods

        public override void ViewDidLayoutSubviews()
        {
            if (_isDisposed)
                return;

            base.ViewDidLayoutSubviews();
            this.UpdateSize();
            PresentedViewController?.ViewDidLayoutSubviews();
        }

        #endregion

        #region Notifications Methods

        private void UnregisterAllObservers()
        {
            if (_willChangeFrameNotificationObserver != null)
                NSNotificationCenter.DefaultCenter.RemoveObserver(_willChangeFrameNotificationObserver);

            if (_willHideNotificationObserver != null)
                NSNotificationCenter.DefaultCenter.RemoveObserver(_willHideNotificationObserver);

            _willChangeFrameNotificationObserver = null;
            _willHideNotificationObserver = null;
        }

        private void KeyBoardUpNotification(NSNotification notifi)
        {
            KeyboardBounds = UIKeyboard.FrameEndFromNotification(notifi);

            ViewDidLayoutSubviews();
        }

        private async void KeyBoardDownNotification(NSNotification notifi)
        {
            NSObject duration = null!;
            var canAnimated = notifi.UserInfo?.TryGetValue(UIKeyboard.AnimationDurationUserInfoKey, out duration);

            KeyboardBounds = CGRect.Empty;

            if (canAnimated ?? false)
            {
                //It is needed that buttons are working when keyboard is opened. See #11
                await Task.Delay(70);

                if (!_isDisposed)
                    await UIView.AnimateAsync((double)(NSNumber)duration, OnKeyboardAnimated);
            }
            else
            {
                ViewDidLayoutSubviews();
            }
        }

        #endregion

        #region Animation Methods

        private void OnKeyboardAnimated()
        {
            if (_isDisposed)
                return;

            ViewDidLayoutSubviews();
        }

        #endregion

        #region Override Methods
        
         public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations()
        {
            if ((ChildViewControllers != null) && (ChildViewControllers.Length > 0))
            {
                return ChildViewControllers[0].GetSupportedInterfaceOrientations();
            }
            return base.GetSupportedInterfaceOrientations();
        }

        public override UIInterfaceOrientation PreferredInterfaceOrientationForPresentation()
        {
            if ((ChildViewControllers != null) && (ChildViewControllers.Length > 0))
            {
                return ChildViewControllers[0].PreferredInterfaceOrientationForPresentation();
            }
            return base.PreferredInterfaceOrientationForPresentation();
        }

        public override UIViewController ChildViewControllerForStatusBarHidden()
        {
            return _pageHandler?.ViewController!;
        }

        public override bool PrefersStatusBarHidden()
        {
            return _pageHandler?.ViewController?.PrefersStatusBarHidden() ?? false;
        }

        public override UIViewController ChildViewControllerForStatusBarStyle()
        {
            return _pageHandler?.ViewController!;
        }

        public override UIStatusBarStyle PreferredStatusBarStyle()
        {
            return (UIStatusBarStyle)(_pageHandler?.ViewController?.PreferredStatusBarStyle())!;
        }

        public override bool ShouldAutorotate()
        {
            if ((ChildViewControllers != null) && (ChildViewControllers.Length > 0))
            {
                return ChildViewControllers[0].ShouldAutorotate();
            }
            return base.ShouldAutorotate();
        }

        public override bool ShouldAutorotateToInterfaceOrientation(UIInterfaceOrientation toInterfaceOrientation)
        {
            if ((ChildViewControllers != null) && (ChildViewControllers.Length > 0))
            {
                return ChildViewControllers[0].ShouldAutorotateToInterfaceOrientation(toInterfaceOrientation);
            }
            return base.ShouldAutorotateToInterfaceOrientation(toInterfaceOrientation);
        }

        public override bool ShouldAutomaticallyForwardRotationMethods => true;

        #endregion
    }
}
