using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

public class CommentSetting : Control
{
	public static readonly DependencyProperty IsCommentSetShowProperty;

	public static readonly DependencyProperty IsEditCommentProperty;

	/// <summary>
	/// 控件圆角半径
	/// </summary>
	public static readonly DependencyProperty CornerRadiusProperty;

	public bool IsCommentSetShow
	{
		get
		{
			return (bool)GetValue(IsCommentSetShowProperty);
		}
		set
		{
			SetValue(IsCommentSetShowProperty, value);
		}
	}

	public bool IsEditComment
	{
		get
		{
			return (bool)GetValue(IsEditCommentProperty);
		}
		set
		{
			SetValue(IsEditCommentProperty, value);
		}
	}

	public CornerRadius CornerRadius
	{
		get
		{
			return (CornerRadius)GetValue(CornerRadiusProperty);
		}
		set
		{
			SetValue(CornerRadiusProperty, value);
		}
	}

	static CommentSetting()
	{
		IsCommentSetShowProperty = DependencyProperty.Register("IsCommentSetShow", typeof(bool), typeof(CommentSetting), new PropertyMetadata(false));
		IsEditCommentProperty = DependencyProperty.Register("IsEditComment", typeof(bool), typeof(CommentSetting), new PropertyMetadata(false));
		CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(CommentSetting), new PropertyMetadata(new CornerRadius(0.0), null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CommentSetting), new FrameworkPropertyMetadata(typeof(CommentSetting)));
	}
}
