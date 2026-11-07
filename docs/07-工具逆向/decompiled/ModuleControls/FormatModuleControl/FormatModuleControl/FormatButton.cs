using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Apps.XmlParser.ParamTab;

namespace FormatModuleControl;

public class FormatButton : Button
{
	public static readonly DependencyProperty TargetProperty = DependencyProperty.Register("Target", typeof(DependencyObject), typeof(FormatButton), new PropertyMetadata((object)null));

	public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(FormatButton), new PropertyMetadata((object)null));

	public static readonly DependencyProperty NormalIconProperty = DependencyProperty.Register("NormalIcon", typeof(ImageSource), typeof(FormatButton), new PropertyMetadata((object)null));

	public static readonly DependencyProperty ClickIconProperty = DependencyProperty.Register("ClickIcon", typeof(ImageSource), typeof(FormatButton), new PropertyMetadata((object)null));

	public static readonly DependencyProperty HoverIconProperty = DependencyProperty.Register("HoverIcon", typeof(ImageSource), typeof(FormatButton), new PropertyMetadata((object)null));

	public DependencyObject Target
	{
		get
		{
			return (DependencyObject)GetValue(TargetProperty);
		}
		set
		{
			SetValue(TargetProperty, value);
		}
	}

	public string Text
	{
		get
		{
			return (string)GetValue(TextProperty);
		}
		set
		{
			SetValue(TextProperty, value);
		}
	}

	public ImageSource NormalIcon
	{
		get
		{
			return (ImageSource)GetValue(NormalIconProperty);
		}
		set
		{
			SetValue(NormalIconProperty, value);
		}
	}

	public ImageSource ClickIcon
	{
		get
		{
			return (ImageSource)GetValue(ClickIconProperty);
		}
		set
		{
			SetValue(ClickIconProperty, value);
		}
	}

	public ImageSource HoverIcon
	{
		get
		{
			return (ImageSource)GetValue(HoverIconProperty);
		}
		set
		{
			SetValue(HoverIconProperty, value);
		}
	}

	protected override void OnClick()
	{
		base.OnClick();
		if (!(DataContext is Format format))
		{
			return;
		}
		MatchEvaluator evaluator = (Match m) => "\\r";
		MatchEvaluator evaluator2 = (Match m) => "\\n";
		FormatTextInfo formatTextInfo = new FormatTextInfo
		{
			TextInfoType = TextType.Symbol
		};
		formatTextInfo.TempValue = Regex.Replace(Regex.Replace(Text, "\r", evaluator), "\n", evaluator2);
		if (format.CurrentRow != null)
		{
			format.CurrentRow.FormatInfoCollection.Add(formatTextInfo);
			formatTextInfo.SetParentNode(format.CurrentRow);
		}
		else if (format.CurrentInfo != null)
		{
			FormatRow parentNode = format.CurrentInfo.GetParentNode<FormatRow>();
			if (parentNode != null)
			{
				formatTextInfo.SetParentNode(parentNode);
				int num = parentNode.FormatInfoCollection.IndexOf(format.CurrentInfo);
				parentNode.FormatInfoCollection.Insert(++num, formatTextInfo);
			}
		}
		format.CurrentRow = null;
		format.CurrentInfo = formatTextInfo;
	}
}
