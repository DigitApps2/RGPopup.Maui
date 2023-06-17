﻿using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Platform;
using RGPopup.Maui.Droid.Gestures;
using RGPopup.Maui.Pages;
using View = Android.Views.View;

namespace RGPopup.Maui.Droid.Renderers
{
    public class PopupPageRenderer : ContentViewGroup
    {
        private static FrameLayout? DecorView => Popup.DecorView;
        private static Thickness _safePadding = new Thickness(50);
        private static double _sizeRatio = 0D;
        private static double _windowWidth = 0D;
        private static double _windowHeight = 0D;
        private static double _contentWidth = 0D;
        private static double _contentHeight = 0D;
        private static double _contentX = 0D;
        private static double _contentY = 0D;
        
        private readonly RgGestureDetectorListener _gestureDetectorListener;
        private readonly GestureDetector _gestureDetector;
        private DateTime _downTime;
        private Point _downPosition;
        private bool _disposed;

        public PopupPage? CurrentElement { get; }
        public Microsoft.Maui.Controls.View? PopupContent { get; }

        #region Main Methods

        public PopupPageRenderer(Context context, IContentView view) : base(context)
        {
            CurrentElement = view as PopupPage;
            PopupContent = ((ContentView?)CurrentElement?.Content)?.Content;
            
            _gestureDetectorListener = new RgGestureDetectorListener();
            _gestureDetectorListener.Clicked += OnBackgroundClick;
            _gestureDetector = new GestureDetector(Context, _gestureDetectorListener);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposed = true;

                _gestureDetectorListener.Clicked -= OnBackgroundClick;
                _gestureDetectorListener.Dispose();
                _gestureDetector.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Layout Methods

        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            var activity = (Activity?)Context;
            var element = CurrentElement;

            if (element == null)
                return;

            Thickness systemPadding;
            var keyboardOffset = 0d;

            var decoreView = activity?.Window?.DecorView;

            var visibleRect = new Android.Graphics.Rect();

            decoreView?.GetWindowVisibleDisplayFrame(visibleRect);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M && RootWindowInsets != null)
            {
                var h = b - t;

                var windowInsets = RootWindowInsets;
                var bottomPadding = Math.Min(windowInsets.StableInsetBottom, windowInsets.SystemWindowInsetBottom);

                if (h - visibleRect.Bottom > windowInsets.StableInsetBottom)
                {
                    keyboardOffset = Context.FromPixels(h - visibleRect.Bottom);
                }

                systemPadding = new Thickness
                {
                    Left = Context.FromPixels(windowInsets.SystemWindowInsetLeft),
                    Top = Context.FromPixels(windowInsets.SystemWindowInsetTop),
                    Right = Context.FromPixels(windowInsets.SystemWindowInsetRight),
                    Bottom = Context.FromPixels(bottomPadding)
                };
            }
            else if (Build.VERSION.SdkInt < BuildVersionCodes.M && decoreView != null)
            {
                var screenSize = new Android.Graphics.Point();
                activity?.WindowManager?.DefaultDisplay?.GetSize(screenSize);

                var keyboardHeight = 0d;

                var decoreHeight = decoreView.Height;
                var decoreWidth = decoreView.Width;

                if (visibleRect.Bottom < screenSize.Y)
                {
                    keyboardHeight = screenSize.Y - visibleRect.Bottom;
                    keyboardOffset = Context.FromPixels(decoreHeight - visibleRect.Bottom);
                }

                systemPadding = new Thickness
                {
                    Left = Context.FromPixels(visibleRect.Left),
                    Top = Context.FromPixels(visibleRect.Top),
                    Right = Context.FromPixels(decoreWidth - visibleRect.Right),
                    Bottom = Context.FromPixels(decoreHeight - visibleRect.Bottom - keyboardHeight)
                };
            }
            else
            {
                systemPadding = new Thickness();
            }

            var needForceLayout =
                (element.HasSystemPadding && element.SystemPadding != systemPadding)
                || (element.HasKeyboardOffset && element.KeyboardOffset != keyboardOffset);

            element.SetValueFromRenderer(PopupPage.SystemPaddingProperty, systemPadding);
            element.SetValueFromRenderer(PopupPage.KeyboardOffsetProperty, keyboardOffset);

            if (changed)
            {
                element.Layout(new Rect(Context.FromPixels(l), Context.FromPixels(t), Context.FromPixels(r), Context.FromPixels(b)));
            }
            else if (needForceLayout)
            {
                element.ForceLayout();
            }
            
            if (CurrentElement != null && DecorView != null)
            {
                _sizeRatio = DecorView.Height / CurrentElement.Height;
                _windowWidth = DecorView.Width;
                _windowHeight = DecorView.Height;

                var safePadding = CurrentElement.SafePadding;
                var gestureInsets = RootWindowInsets.SystemGestureInsets;
                _safePadding.Left = Math.Max(safePadding.Left * _sizeRatio, gestureInsets.Left);
                _safePadding.Right = Math.Max(safePadding.Right * _sizeRatio, gestureInsets.Right);
                _safePadding.Top = Math.Max(safePadding.Top * _sizeRatio, gestureInsets.Top);
                _safePadding.Bottom = Math.Max(safePadding.Bottom * _sizeRatio, gestureInsets.Bottom);
            }
            
            base.OnLayout(changed, l, t, r, b);
        }

        #endregion

        #region Life Cycle Methods

        protected override void OnAttachedToWindow()
        {
            Context?.HideKeyboard(DecorView);
            base.OnAttachedToWindow();
        }

        protected override void OnDetachedFromWindow()
        {
            CurrentElement?.Dispatcher.StartTimer(TimeSpan.FromMilliseconds(0), () =>
            {
                Context?.HideKeyboard(DecorView);
                return false;
            });
            base.OnDetachedFromWindow();
        }

        protected override void OnWindowVisibilityChanged(ViewStates visibility)
        {
            base.OnWindowVisibilityChanged(visibility);

            // It is needed because a size of popup has not updated on Android 7+. See #209
            if (visibility == ViewStates.Visible)
                RequestLayout();
        }

        #endregion

        #region Touch Methods

        public override bool DispatchTouchEvent(MotionEvent? e)
        {
            if (e == null){ return false; }
            
            if (e.Action == MotionEventActions.Down)
            {
                _downTime = DateTime.UtcNow;
                _downPosition = new Point(e.RawX, e.RawY);
            }
            if (e.Action != MotionEventActions.Up)
                return base.DispatchTouchEvent(e);

            if (_disposed)
                return false;

            View? currentFocus1 = ((Activity?)Context)?.CurrentFocus;

            if (currentFocus1 is EditText)
            {
                View? currentFocus2 = ((Activity?)Context)?.CurrentFocus;
                if (currentFocus1 == currentFocus2 && _downPosition.Distance(new Point(e.RawX, e.RawY)) <= Context.ToPixels(20.0) && !(DateTime.UtcNow - _downTime > TimeSpan.FromMilliseconds(200.0)))
                {
                    var location = new int[2];
                    currentFocus1.GetLocationOnScreen(location);
                    var num1 = e.RawX + currentFocus1.Left - location[0];
                    var num2 = e.RawY + currentFocus1.Top - location[1];
                    if (!new Rect(currentFocus1.Left, currentFocus1.Top, currentFocus1.Width, currentFocus1.Height).Contains(num1, num2))
                    {
                        Context.HideKeyboard(currentFocus1);
                        currentFocus1.ClearFocus();
                    }
                }
            }

            if (_disposed)
                return false;

            var flag = base.DispatchTouchEvent(e);

            return flag;
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (_disposed || e == null || CurrentElement == null || PopupContent == null)
                return false;

            var baseValue = base.OnTouchEvent(e);

            _gestureDetector.OnTouchEvent(e);

            var hasCommand = CurrentElement.BackgroundClickedCommand != null;
            if (hasCommand || CurrentElement.BackgroundInputTransparent || CurrentElement.CloseWhenBackgroundIsClicked)
            {
                _contentWidth = PopupContent.Width * _sizeRatio;
                _contentHeight = PopupContent.Height * _sizeRatio;
                _contentX = PopupContent.X * _sizeRatio;
                _contentY = PopupContent.Y * _sizeRatio;

                var isInRegion = IsInRegion(e.RawX, e.RawY);
                var isInSafePadding = !isInRegion && IsInSafePadding(e.RawX, e.RawY);
                if ((hasCommand && !isInSafePadding) || (ChildCount > 0 && !isInRegion && !isInSafePadding) || ChildCount == 0)
                {
                    _ = CurrentElement.SendBackgroundClick();
                    if (!CurrentElement.BackgroundInputTransparent)
                    {
                        //Prevent other view handle the click event.
                        return true;
                    }
                }
                else if (isInSafePadding)
                {
                    //Prevent other view handle the click event.
                    return true;
                }
            }

            return baseValue;
        }

        private void OnBackgroundClick(object? sender, MotionEvent? e)
        {
            if (ChildCount == 0 || e == null)
                return;

            //var isInRegion = IsInRegion(e.RawX, e.RawY, GetChildAt(0)!);
            var isInRegion = IsInRegion(e.RawX, e.RawY);
            var isInSafePadding = IsInSafePadding(e.RawX, e.RawY);
            if (!isInRegion && !isInSafePadding)
                CurrentElement?.SendBackgroundClick();
        }

        // Fix for "CloseWhenBackgroundIsClicked not works on Android with Xamarin.Forms 2.4.0.280" #173
        private static bool IsInRegion(float x, float y, View v)
        {
            var mCoordBuffer = new int[2];

            v.GetLocationOnScreen(mCoordBuffer);
            return mCoordBuffer[0] + v.Width > x &&    // right edge
                   mCoordBuffer[1] + v.Height > y &&   // bottom edge
                   mCoordBuffer[0] < x &&              // left edge
                   mCoordBuffer[1] < y;                // top edge
        }
        
        private static bool IsInRegion(float x, float y)
        {
            var inViewRegion = 
                 _contentX + _contentWidth > x &&    // right edge
                 _contentY + _contentHeight > y &&   // bottom edge
                 _contentX < x &&                    // left edge
                 _contentY < y;                      // top edge
            return inViewRegion;
        }

        private static bool IsInSafePadding(float x, float y)
        {
            var inSafePadding =  !(x > _safePadding.Left
                                   && x < (_windowWidth - _safePadding.Right)
                                   && y > _safePadding.Top
                                   && y < (_windowHeight - _safePadding.Bottom));
            return inSafePadding;
        }

        #endregion
    }
}
