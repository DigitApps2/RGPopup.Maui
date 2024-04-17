using Foundation;
using Microsoft.Maui.Controls.Platform;
using CoreGraphics;
using UIKit;

namespace RGPopup.Maui.Effects;
public class KeyboardOverlapFixPlatformEffect : PlatformEffect
{
    private const double KeyboardOverlapAdjust = 0;
    private static readonly bool IsIOS15 = DeviceInfo.Version.Major >= 15;
    
    private UIView? _responderView;
    private NSObject? _keyboardShowObserver;
    private NSObject? _keyboardShownObserver;
    private NSObject? _keyboardHideObserver;
    private Thickness? _originalPadding;
    private Thickness _currentPadding;
    private double _keyboardOverlap;
    private nfloat _keyboardHeight;
    private bool _keyboardShown;
    private bool _pageShiftedUp;
    private bool _pageLoaded = false;
    
    private ContentPage? CurrentPage => Element as ContentPage;
    
    protected override void OnAttached()
    {
        if (CurrentPage == null) return;
        _pageLoaded = true;
        RegisterForKeyboardNotifications();
    }

    protected override void OnDetached()
    {
        _pageLoaded = false;
        UnregisterForKeyboardNotifications();
    }
    
    private void RegisterForKeyboardNotifications()
    {
        _keyboardShowObserver ??=
            NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillShowNotification, OnKeyboardShow);
        _keyboardShownObserver ??=
            NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.DidShowNotification, OnKeyboardShown);
        _keyboardHideObserver ??=
            NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillHideNotification, OnKeyboardHide);
    }

    private void UnregisterForKeyboardNotifications()
    {
        if (_keyboardShowObserver != null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_keyboardShowObserver);
            _keyboardShowObserver.Dispose();
            _keyboardShowObserver = null;
        }
        if (_keyboardShownObserver != null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_keyboardShownObserver);
            _keyboardShownObserver.Dispose();
            _keyboardShownObserver = null;
        }
        if (_keyboardHideObserver != null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_keyboardHideObserver);
            _keyboardHideObserver.Dispose();
            _keyboardHideObserver = null;
        }
    }

    private void OnKeyboardShow(NSNotification notification)
    {
        if (!_pageLoaded) return;

        var keyboardFrame = UIKeyboard.FrameEndFromNotification(notification);
        var keyboardHeight = keyboardFrame.Height;
        //Console.WriteLine($"KeyboardWillShowHeight: {keyboardHeight}");
        if (keyboardHeight <= _keyboardHeight) return;
        this.CheckOverlap(keyboardHeight);
    }
    
    private void OnKeyboardShown(NSNotification notification)
    {
        if (!_pageLoaded || _responderView == null || _keyboardShown) return;

        _keyboardShown = true;
        var keyboardHeight = IsIOS15 ? _responderView.KeyboardLayoutGuide.LayoutFrame.Height : 0;
        if (keyboardHeight <= 0)
        {
            var keyboardFrame = UIKeyboard.FrameEndFromNotification(notification);
            keyboardHeight = keyboardFrame.Height;
        }
        
        //Console.WriteLine($"KeyboardDidShowHeight: {keyboardHeight}");
        if (keyboardHeight <= _keyboardHeight) return;
        this.CheckOverlap(keyboardHeight);
    }

    private void OnKeyboardHide(NSNotification notification)
    {
        if (!_pageLoaded) return;
        _keyboardHeight = 0;
        _keyboardOverlap = 0;
        _keyboardShown = false;
        _responderView = null;
        if (_pageShiftedUp)
        {
            ShiftPageDown();
        }
    }

    private void CheckOverlap(nfloat keyboardHeight)
    {
        if (_pageShiftedUp) { return; }
        var deltaHeight = keyboardHeight - _keyboardHeight;
        if (_keyboardOverlap > 0 && deltaHeight > KeyboardOverlapAdjust)
        {
            _keyboardOverlap += deltaHeight;
            _keyboardHeight = keyboardHeight;
            this.ShiftPageUp();
            return;
        }
        
        _keyboardHeight = keyboardHeight;
        _responderView ??= FindFirstResponder(Control);
        if (_responderView == null) return;
        _keyboardOverlap = GetOverlapDistance(_responderView, Control, _keyboardHeight, false);
        //Console.WriteLine($"KeyboardOverlap: {_keyboardOverlap}");
        if (_keyboardOverlap > 0)
        {
            ShiftPageUp();
        }
    }

    private void ShiftPageUp()
    {
        if (CurrentPage == null || Control == null) return;
        var deltaBottom = _keyboardOverlap + KeyboardOverlapAdjust;
        _originalPadding ??= CurrentPage.Padding;
        _currentPadding = new Thickness(_originalPadding.Value.Left, _originalPadding.Value.Top, _originalPadding.Value.Right, _originalPadding.Value.Bottom + deltaBottom);
        CurrentPage.Dispatcher.Dispatch(() =>
        {
            CurrentPage.Padding = _currentPadding;
        });
        _pageShiftedUp = true;
    }

    private void ShiftPageDown()
    {
        if (CurrentPage == null || _originalPadding == null) return;
        CurrentPage.Dispatcher.Dispatch(() =>
        {
            CurrentPage.Padding = _originalPadding.Value;
        });
        _pageShiftedUp = false;
    }
    
    public static UIView? FindFirstResponder(UIView? view)
    {
        if (view == null || view.IsFirstResponder)
        {
            return view;
        }
        foreach (var subView in view.Subviews)
        {
            var firstResponder = FindFirstResponder(subView);
            if (firstResponder != null)
            {
                return firstResponder;
            }
        }
        return null;
    }
    
    public static double GetViewRelativeBottom(UIView view, UIView rootView)
    {
        // https://developer.apple.com/documentation/uikit/uiview/1622424-convertpoint
        var viewRelativeCoordinates = rootView.ConvertPointFromView(new CGPoint(0, 0), view);
        var activeViewRoundedY = Math.Round(viewRelativeCoordinates.Y, 2);
        return activeViewRoundedY + view.Frame.Height;
    }
    
    public static double GetOverlapDistance(UIView activeView, UIView rootView, nfloat keyboardHeight, bool useSafeArea)
    {
        double bottom = GetViewRelativeBottom(activeView, rootView);
        return GetOverlapDistance(bottom, rootView, keyboardHeight, useSafeArea);
    }
    
    private static double GetOverlapDistance(double relativeBottom, UIView rootView, nfloat keyboardHeight, bool useSafeArea)
    {
        var safeAreaBottom = useSafeArea ? rootView.Window.SafeAreaInsets.Bottom : 0;
        var pageHeight = rootView.Frame.Height;
        //Console.WriteLine($"relativeBottom:{relativeBottom}, pageHeight:{pageHeight}, keyboardHeight:{keyboardHeight}");
        return relativeBottom - (pageHeight + safeAreaBottom - keyboardHeight);
    }
}