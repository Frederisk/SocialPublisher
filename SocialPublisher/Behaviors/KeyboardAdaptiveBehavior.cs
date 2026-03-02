using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Xaml.Interactivity;

namespace SocialPublisher.Behaviors;

public class KeyboardAdaptiveBehavior : Behavior<Control> {
    //public static readonly StyledProperty<Boolean> IsEnabledProperty = AvaloniaProperty.Register<KeyboardAdaptiveBehavior, Boolean>(nameof(IsEnabled), true);

    //public Boolean IsEnabled {
    //    get => this.GetValue(IsEnabledProperty);
    //    set => this.SetValue(IsEnabledProperty, value);
    //}

    private IInputPane? _inputPane;

    protected override void OnAttached() {
        base.OnAttached();

        if (this.AssociatedObject is null) {
            return;
        }

        this.AssociatedObject.AttachedToVisualTree += this.OnAttachedToVisualTree;
        this.AssociatedObject.DetachedFromVisualTree += this.OnDetachedFromVisualTree;
    }

    protected override void OnDetaching() {
        if (this.AssociatedObject is not null) {
            this.AssociatedObject.AttachedToVisualTree -= this.OnAttachedToVisualTree;
            this.AssociatedObject.DetachedFromVisualTree -= this.OnDetachedFromVisualTree;
        }
        this.Unsubscribe();
        base.OnDetaching();
    }

    private void OnAttachedToVisualTree(Object? sender, VisualTreeAttachmentEventArgs args) {
        var topLevel = TopLevel.GetTopLevel(this.AssociatedObject);
        if (topLevel?.InputPane is { } inputPane) {
            _inputPane = inputPane;
            _inputPane.StateChanged += this.OnInputPaneStateChanged;
        }
    }

    private void OnDetachedFromVisualTree(Object? sender, VisualTreeAttachmentEventArgs args) {
        this.Unsubscribe();
    }

    private void Unsubscribe() {
        _inputPane?.StateChanged -= this.OnInputPaneStateChanged;
        _inputPane = null;
    }

    private void OnInputPaneStateChanged(Object? sender, InputPaneStateEventArgs args) {
        if (this.AssociatedObject is null || !this.IsEnabled) {
            return;
        }
        var height = args.NewState == InputPaneState.Open ? _inputPane?.OccludedRect.Height ?? 0 : 0;
        this.AssociatedObject.Margin = new Thickness(
            this.AssociatedObject.Margin.Left,
            this.AssociatedObject.Margin.Top,
            this.AssociatedObject.Margin.Right,
            height);

        if (args.NewState == InputPaneState.Open) {
            var focused = TopLevel.GetTopLevel(this.AssociatedObject)?.FocusManager?.GetFocusedElement() as Control;
            focused?.BringIntoView();
        }
    }
}
