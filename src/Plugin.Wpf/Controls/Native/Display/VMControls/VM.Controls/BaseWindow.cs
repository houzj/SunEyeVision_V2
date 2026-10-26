using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Native.Display.Apps.ColorFun;
using Apps.Localization;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "MinWinButton", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "MaxWinButton", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "CloseWinButton", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "Header", Type = typeof(HeaderedContentControl))]
[TemplatePart(Name = "Border", Type = typeof(Border))]
[TemplatePart(Name = "Title", Type = typeof(TextBlock))]
[TemplatePart(Name = "TitleBar", Type = typeof(ContentControl))]
public class BaseWindow : Window, INotifyPropertyChanged
{
	public enum WindowCloseReason
	{
		None,
		WindowsShutDown,
		MdiFormClosing,
		UserClosing,
		TaskManagerClosing,
		FormOwnerClosing,
		ApplicationExitCall
	}

	public struct WINDOWPLACEMENT
	{
		public int length;

		public int flags;

		public int showCmd;

		public int ptMinPosition_x;

		public int ptMinPosition_y;

		public int ptMaxPosition_x;

		public int ptMaxPosition_y;

		public int rcNormalPosition_left;

		public int rcNormalPosition_top;

		public int rcNormalPosition_right;

		public int rcNormalPosition_bottom;
	}

	public enum ResizeDirection
	{
		Left = 1,
		Right = 2,
		Bottom = 6,
		BottomLeft = 7,
		BottomRight = 8
	}

	public struct Point(int x, int y)
	{
		public int x = x;

		public int y = y;
	}

	private enum MonitorOptions : uint
	{
		MONITOR_DEFAULTTONULL,
		MONITOR_DEFAULTTOPRIMARY,
		MONITOR_DEFAULTTONEAREST
	}

	public static class SYSTEMCOMMAND
	{
		public const int SC_SIZE = 61440;

		public const int SC_MOVE = 61456;

		public const int SC_MINIMIZE = 61472;

		public const int SC_MAXIMIZE = 61488;

		public const int SC_NEXTWINDOW = 61504;

		public const int SC_PREVWINDOW = 61520;

		public const int SC_CLOSE = 61536;

		public const int SC_VSCROLL = 61552;

		public const int SC_HSCROLL = 61568;

		public const int SC_MOUSEMENU = 61584;

		public const int SC_KEYMENU = 61696;

		public const int SC_ARRANGE = 61712;

		public const int SC_RESTORE = 61728;

		public const int SC_TASKLIST = 61744;

		public const int SC_SCREENSAVE = 61760;

		public const int SC_HOTKEY = 61776;

		public const int SC_DEFAULT = 61792;

		public const int SC_MONITORPOWER = 61808;

		public const int SC_CONTEXTHELP = 61824;

		public const int SC_SEPARATOR = 61455;

		public const int SC_ICON = 61472;

		public const int SC_ZOOM = 61488;
	}

	public static class WINDOWS_MESSAGE
	{
		private static Dictionary<int, string> MESSAGE;

		public const int WM_NULL = 0;

		public const int WM_CREATE = 1;

		public const int WM_DESTROY = 2;

		public const int WM_MOVE = 3;

		public const int WM_SIZE = 5;

		public const int WM_ACTIVATE = 6;

		public const int WM_SETFOCUS = 7;

		public const int WM_KILLFOCUS = 8;

		public const int WM_ENABLE = 10;

		public const int WM_SETREDRAW = 11;

		public const int WM_SETTEXT = 12;

		public const int WM_GETTEXT = 13;

		public const int WM_GETTEXTLENGTH = 14;

		public const int WM_PAINT = 15;

		public const int WM_CLOSE = 16;

		public const int WM_QUERYENDSESSION = 17;

		public const int WM_QUERYOPEN = 19;

		public const int WM_ENDSESSION = 22;

		public const int WM_QUIT = 18;

		public const int WM_ERASEBKGND = 20;

		public const int WM_SYSCOLORCHANGE = 21;

		public const int WM_SHOWWINDOW = 24;

		public const int WM_WININICHANGE = 26;

		public const int WM_SETTINGCHANGE = 26;

		public const int WM_DEVMODECHANGE = 27;

		public const int WM_ACTIVATEAPP = 28;

		public const int WM_FONTCHANGE = 29;

		public const int WM_TIMECHANGE = 30;

		public const int WM_CANCELMODE = 31;

		public const int WM_SETCURSOR = 32;

		public const int WM_MOUSEACTIVATE = 33;

		public const int WM_CHILDACTIVATE = 34;

		public const int WM_QUEUESYNC = 35;

		public const int WM_GETMINMAXINFO = 36;

		public const int WM_PAINTICON = 38;

		public const int WM_ICONERASEBKGND = 39;

		public const int WM_NEXTDLGCTL = 40;

		public const int WM_SPOOLERSTATUS = 42;

		public const int WM_DRAWITEM = 43;

		public const int WM_MEASUREITEM = 44;

		public const int WM_DELETEITEM = 45;

		public const int WM_VKEYTOITEM = 46;

		public const int WM_CHARTOITEM = 47;

		public const int WM_SETFONT = 48;

		public const int WM_GETFONT = 49;

		public const int WM_SETHOTKEY = 50;

		public const int WM_GETHOTKEY = 51;

		public const int WM_QUERYDRAGICON = 55;

		public const int WM_COMPAREITEM = 57;

		public const int WM_GETOBJECT = 61;

		public const int WM_COMPACTING = 65;

		public const int WM_COMMNOTIFY = 68;

		public const int WM_WINDOWPOSCHANGING = 70;

		public const int WM_WINDOWPOSCHANGED = 71;

		public const int WM_POWER = 72;

		public const int WM_COPYDATA = 74;

		public const int WM_CANCELJOURNAL = 75;

		public const int WM_NOTIFY = 78;

		public const int WM_INPUTLANGCHANGEREQUEST = 80;

		public const int WM_INPUTLANGCHANGE = 81;

		public const int WM_TCARD = 82;

		public const int WM_HELP = 83;

		public const int WM_USERCHANGED = 84;

		public const int WM_NOTIFYFORMAT = 85;

		public const int WM_CONTEXTMENU = 123;

		public const int WM_STYLECHANGING = 124;

		public const int WM_STYLECHANGED = 125;

		public const int WM_DISPLAYCHANGE = 126;

		public const int WM_GETICON = 127;

		public const int WM_SETICON = 128;

		public const int WM_NCCREATE = 129;

		public const int WM_NCDESTROY = 130;

		public const int WM_NCCALCSIZE = 131;

		public const int WM_NCHITTEST = 132;

		public const int WM_NCPAINT = 133;

		public const int WM_NCACTIVATE = 134;

		public const int WM_GETDLGCODE = 135;

		public const int WM_SYNCPAINT = 136;

		public const int WM_NCMOUSEMOVE = 160;

		public const int WM_NCLBUTTONDOWN = 161;

		public const int WM_NCLBUTTONUP = 162;

		public const int WM_NCLBUTTONDBLCLK = 163;

		public const int WM_NCRBUTTONDOWN = 164;

		public const int WM_NCRBUTTONUP = 165;

		public const int WM_NCRBUTTONDBLCLK = 166;

		public const int WM_NCMBUTTONDOWN = 167;

		public const int WM_NCMBUTTONUP = 168;

		public const int WM_NCMBUTTONDBLCLK = 169;

		public const int WM_NCXBUTTONDOWN = 171;

		public const int WM_NCXBUTTONUP = 172;

		public const int WM_NCXBUTTONDBLCLK = 173;

		public const int WM_INPUT = 255;

		public const int WM_KEYFIRST = 256;

		public const int WM_KEYDOWN = 256;

		public const int WM_KEYUP = 257;

		public const int WM_CHAR = 258;

		public const int WM_DEADCHAR = 259;

		public const int WM_SYSKEYDOWN = 260;

		public const int WM_SYSKEYUP = 261;

		public const int WM_SYSCHAR = 262;

		public const int WM_SYSDEADCHAR = 263;

		public const int WM_KEYLAST = 265;

		public const int WM_UNICHAR = 265;

		public const int WM_IME_STARTCOMPOSITION = 269;

		public const int WM_IME_ENDCOMPOSITION = 270;

		public const int WM_IME_COMPOSITION = 271;

		public const int WM_IME_KEYLAST = 271;

		public const int WM_INITDIALOG = 272;

		public const int WM_COMMAND = 273;

		public const int WM_SYSCOMMAND = 274;

		public const int WM_TIMER = 275;

		public const int WM_HSCROLL = 276;

		public const int WM_VSCROLL = 277;

		public const int WM_INITMENU = 278;

		public const int WM_INITMENUPOPUP = 279;

		public const int WM_MENUSELECT = 287;

		public const int WM_MENUCHAR = 288;

		public const int WM_ENTERIDLE = 289;

		public const int WM_MENURBUTTONUP = 290;

		public const int WM_MENUDRAG = 291;

		public const int WM_MENUGETOBJECT = 292;

		public const int WM_UNINITMENUPOPUP = 293;

		public const int WM_MENUCOMMAND = 294;

		public const int WM_CHANGEUISTATE = 295;

		public const int WM_UPDATEUISTATE = 296;

		public const int WM_QUERYUISTATE = 297;

		public const int WM_CTLCOLORMSGBOX = 306;

		public const int WM_CTLCOLOREDIT = 307;

		public const int WM_CTLCOLORLISTBOX = 308;

		public const int WM_CTLCOLORBTN = 309;

		public const int WM_CTLCOLORDLG = 310;

		public const int WM_CTLCOLORSCROLLBAR = 311;

		public const int WM_CTLCOLORSTATIC = 312;

		public const int WM_MOUSEFIRST = 512;

		public const int WM_MOUSEMOVE = 512;

		public const int WM_LBUTTONDOWN = 513;

		public const int WM_LBUTTONUP = 514;

		public const int WM_LBUTTONDBLCLK = 515;

		public const int WM_RBUTTONDOWN = 516;

		public const int WM_RBUTTONUP = 517;

		public const int WM_RBUTTONDBLCLK = 518;

		public const int WM_MBUTTONDOWN = 519;

		public const int WM_MBUTTONUP = 520;

		public const int WM_MBUTTONDBLCLK = 521;

		public const int WM_MOUSELAST95 = 521;

		public const int WM_MOUSEWHEEL = 522;

		public const int WM_MOUSELAST_NT4_98 = 522;

		public const int WM_XBUTTONDOWN = 523;

		public const int WM_XBUTTONUP = 524;

		public const int WM_XBUTTONDBLCLK = 525;

		public const int WM_MOUSELAST_2K_XP_2k3 = 525;

		public const int WM_PARENTNOTIFY = 528;

		public const int WM_ENTERMENULOOP = 529;

		public const int WM_EXITMENULOOP = 530;

		public const int WM_NEXTMENU = 531;

		public const int WM_SIZING = 532;

		public const int WM_CAPTURECHANGED = 533;

		public const int WM_MOVING = 534;

		public const int WM_POWERBROADCAST = 536;

		public const int WM_DEVICECHANGE = 537;

		public const int WM_MDICREATE = 544;

		public const int WM_MDIDESTROY = 545;

		public const int WM_MDIACTIVATE = 546;

		public const int WM_MDIRESTORE = 547;

		public const int WM_MDINEXT = 548;

		public const int WM_MDIMAXIMIZE = 549;

		public const int WM_MDITILE = 550;

		public const int WM_MDICASCADE = 551;

		public const int WM_MDIICONARRANGE = 552;

		public const int WM_MDIGETACTIVE = 553;

		public const int WM_MDISETMENU = 560;

		public const int WM_ENTERSIZEMOVE = 561;

		public const int WM_EXITSIZEMOVE = 562;

		public const int WM_DROPFILES = 563;

		public const int WM_MDIREFRESHMENU = 564;

		public const int WM_IME_SETCONTEXT = 641;

		public const int WM_IME_NOTIFY = 642;

		public const int WM_IME_CONTROL = 643;

		public const int WM_IME_COMPOSITIONFULL = 644;

		public const int WM_IME_SELECT = 645;

		public const int WM_IME_CHAR = 646;

		public const int WM_IME_REQUEST = 648;

		public const int WM_IME_KEYDOWN = 656;

		public const int WM_IME_KEYUP = 657;

		public const int WM_MOUSEHOVER = 673;

		public const int WM_MOUSELEAVE = 675;

		public const int WM_NCMOUSEHOVER = 672;

		public const int WM_NCMOUSELEAVE = 674;

		public const int WM_WTSSESSION_CHANGE = 689;

		public const int WM_TABLET_FIRST = 704;

		public const int WM_TABLET_LAST = 735;

		public const int WM_CUT = 768;

		public const int WM_COPY = 769;

		public const int WM_PASTE = 770;

		public const int WM_CLEAR = 771;

		public const int WM_UNDO = 772;

		public const int WM_RENDERFORMAT = 773;

		public const int WM_RENDERALLFORMATS = 774;

		public const int WM_DESTROYCLIPBOARD = 775;

		public const int WM_DRAWCLIPBOARD = 776;

		public const int WM_PAINTCLIPBOARD = 777;

		public const int WM_VSCROLLCLIPBOARD = 778;

		public const int WM_SIZECLIPBOARD = 779;

		public const int WM_ASKCBFORMATNAME = 780;

		public const int WM_CHANGECBCHAIN = 781;

		public const int WM_HSCROLLCLIPBOARD = 782;

		public const int WM_QUERYNEWPALETTE = 783;

		public const int WM_PALETTEISCHANGING = 784;

		public const int WM_PALETTECHANGED = 785;

		public const int WM_HOTKEY = 786;

		public const int WM_PRINT = 791;

		public const int WM_PRINTCLIENT = 792;

		public const int WM_APPCOMMAND = 793;

		public const int WM_THEMECHANGED = 794;

		public const int WM_HANDHELDFIRST = 856;

		public const int WM_HANDHELDLAST = 863;

		public const int WM_AFXFIRST = 864;

		public const int WM_AFXLAST = 895;

		public const int WM_PENWINFIRST = 896;

		public const int WM_PENWINLAST = 911;

		public const int WM_USER = 1024;

		public const int WM_APP = 32768;

		public static string GetMessage(int msg_code)
		{
			if (!MESSAGE.ContainsKey(msg_code))
			{
				return string.Empty;
			}
			return MESSAGE[msg_code];
		}

		static WINDOWS_MESSAGE()
		{
			MESSAGE = new Dictionary<int, string>();
			FieldInfo[] fields = typeof(WINDOWS_MESSAGE).GetFields(BindingFlags.Static | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				int num = (int)fieldInfo.GetValue(null);
				if (MESSAGE.ContainsKey(num))
				{
					Dictionary<int, string> mESSAGE = MESSAGE;
					int key = num;
					mESSAGE[key] = mESSAGE[key] + "|" + fieldInfo.Name;
				}
				else
				{
					MESSAGE.Add(num, fieldInfo.Name);
				}
			}
		}
	}

	private static class SWP_CODE
	{
		internal const uint SWP_DRAWFRAME = 32u;

		internal const uint SWP_FRAMECHANGED = 32u;

		internal const uint SWP_HIDEWINDOW = 128u;

		internal const uint SWP_NOACTIVATE = 16u;

		internal const uint SWP_NOCOPYBITS = 256u;

		internal const uint SWP_NOMOVE = 2u;

		internal const uint SWP_NOOWNERZORDER = 512u;

		internal const uint SWP_NOREDRAW = 8u;

		internal const uint SWP_NOREPOSITION = 512u;

		internal const uint SWP_NOSENDCHANGING = 1024u;

		internal const uint SWP_NOSIZE = 1u;

		internal const uint SWP_NOZORDER = 4u;

		internal const uint SWP_SHOWWINDOW = 64u;
	}

	public static class AccessibilityConstants
	{
		internal const uint OBJID_WINDOW = 0u;

		internal const uint OBJID_SELF = 0u;

		internal const uint OBJID_SYSMENU = uint.MaxValue;

		internal const uint OBJID_TITLEBAR = 4294967294u;

		internal const uint OBJID_MENU = 4294967293u;

		internal const uint OBJID_CLIENT = 4294967292u;

		internal const uint OBJID_VSCROLL = 4294967291u;

		internal const uint OBJID_HSCROLL = 4294967290u;

		internal const uint OBJID_SIZEGRIP = 4294967289u;

		internal const uint OBJID_CARET = 4294967288u;

		internal const uint OBJID_CURSOR = 4294967287u;

		internal const uint OBJID_ALERT = 4294967286u;

		internal const uint OBJID_SOUND = 4294967285u;

		internal const uint OBJID_QUERYCLASSNAMEIDX = 4294967284u;

		internal const uint OBJID_NATIVEOM = 4294967280u;

		internal const uint NAVDIR_UP = 1u;

		internal const uint NAVDIR_DOWN = 2u;

		internal const uint NAVDIR_LEFT = 3u;

		internal const uint NAVDIR_RIGHT = 4u;

		internal const uint NAVDIR_NEXT = 5u;

		internal const uint NAVDIR_PREVIOUS = 6u;

		internal const uint NAVDIR_FIRSTCHILD = 7u;

		internal const uint NAVDIR_LASTCHILD = 8u;

		internal const uint SELFLAG_NONE = 0u;

		internal const uint SELFLAG_TAKEFOCUS = 1u;

		internal const uint SELFLAG_TAKESELECTION = 2u;

		internal const uint SELFLAG_EXTENDSELECTION = 4u;

		internal const uint SELFLAG_ADDSELECTION = 8u;

		internal const uint SELFLAG_REMOVESELECTION = 16u;

		internal const uint SELFLAG_VALID = 31u;

		internal const uint STATE_SYSTEM_UNAVAILABLE = 1u;

		internal const uint STATE_SYSTEM_SELECTED = 2u;

		internal const uint STATE_SYSTEM_FOCUSED = 4u;

		internal const uint STATE_SYSTEM_PRESSED = 8u;

		internal const uint STATE_SYSTEM_CHECKED = 16u;

		internal const uint STATE_SYSTEM_MIXED = 32u;

		internal const uint STATE_SYSTEM_READONLY = 64u;

		internal const uint STATE_SYSTEM_HOTTRACKED = 128u;

		internal const uint STATE_SYSTEM_DEFAULT = 256u;

		internal const uint STATE_SYSTEM_EXPANDED = 512u;

		internal const uint STATE_SYSTEM_COLLAPSED = 1024u;

		internal const uint STATE_SYSTEM_BUSY = 2048u;

		internal const uint STATE_SYSTEM_FLOATING = 4096u;

		internal const uint STATE_SYSTEM_MARQUEED = 8192u;

		internal const uint STATE_SYSTEM_ANIMATED = 16384u;

		internal const uint STATE_SYSTEM_INVISIBLE = 32768u;

		internal const uint STATE_SYSTEM_OFFSCREEN = 65536u;

		internal const uint STATE_SYSTEM_SIZEABLE = 131072u;

		internal const uint STATE_SYSTEM_MOVEABLE = 262144u;

		internal const uint STATE_SYSTEM_SELFVOICING = 524288u;

		internal const uint STATE_SYSTEM_FOCUSABLE = 1048576u;

		internal const uint STATE_SYSTEM_SELECTABLE = 2097152u;

		internal const uint STATE_SYSTEM_LINKED = 4194304u;

		internal const uint STATE_SYSTEM_TRAVERSED = 8388608u;

		internal const uint STATE_SYSTEM_MULTISELECTABLE = 16777216u;

		internal const uint STATE_SYSTEM_EXTSELECTABLE = 33554432u;

		internal const uint STATE_SYSTEM_ALERT_LOW = 67108864u;

		internal const uint STATE_SYSTEM_ALERT_MEDIUM = 134217728u;

		internal const uint STATE_SYSTEM_ALERT_HIGH = 268435456u;

		internal const uint STATE_SYSTEM_VALID = 536870911u;

		internal const uint ROLE_SYSTEM_TITLEBAR = 1u;

		internal const uint ROLE_SYSTEM_MENUBAR = 2u;

		internal const uint ROLE_SYSTEM_SCROLLBAR = 3u;

		internal const uint ROLE_SYSTEM_GRIP = 4u;

		internal const uint ROLE_SYSTEM_SOUND = 5u;

		internal const uint ROLE_SYSTEM_CURSOR = 6u;

		internal const uint ROLE_SYSTEM_CARET = 7u;

		internal const uint ROLE_SYSTEM_ALERT = 8u;

		internal const uint ROLE_SYSTEM_WINDOW = 9u;

		internal const uint ROLE_SYSTEM_CLIENT = 10u;

		internal const uint ROLE_SYSTEM_MENUPOPUP = 11u;

		internal const uint ROLE_SYSTEM_MENUITEM = 12u;

		internal const uint ROLE_SYSTEM_TOOLTIP = 13u;

		internal const uint ROLE_SYSTEM_APPLICATION = 14u;

		internal const uint ROLE_SYSTEM_DOCUMENT = 15u;

		internal const uint ROLE_SYSTEM_PANE = 16u;

		internal const uint ROLE_SYSTEM_CHART = 17u;

		internal const uint ROLE_SYSTEM_DIALOG = 18u;

		internal const uint ROLE_SYSTEM_BORDER = 19u;

		internal const uint ROLE_SYSTEM_GROUPING = 20u;

		internal const uint ROLE_SYSTEM_SEPARATOR = 21u;

		internal const uint ROLE_SYSTEM_TOOLBAR = 22u;

		internal const uint ROLE_SYSTEM_STATUSBAR = 23u;

		internal const uint ROLE_SYSTEM_TABLE = 24u;

		internal const uint ROLE_SYSTEM_COLUMNHEADER = 25u;

		internal const uint ROLE_SYSTEM_ROWHEADER = 26u;

		internal const uint ROLE_SYSTEM_COLUMN = 27u;

		internal const uint ROLE_SYSTEM_ROW = 28u;

		internal const uint ROLE_SYSTEM_CELL = 29u;

		internal const uint ROLE_SYSTEM_LINK = 30u;

		internal const uint ROLE_SYSTEM_HELPBALLOON = 31u;

		internal const uint ROLE_SYSTEM_CHARACTER = 32u;

		internal const uint ROLE_SYSTEM_LIST = 33u;

		internal const uint ROLE_SYSTEM_LISTITEM = 34u;

		internal const uint ROLE_SYSTEM_OUTLINE = 35u;

		internal const uint ROLE_SYSTEM_OUTLINEITEM = 36u;

		internal const uint ROLE_SYSTEM_PAGETAB = 37u;

		internal const uint ROLE_SYSTEM_PROPERTYPAGE = 38u;

		internal const uint ROLE_SYSTEM_INDICATOR = 39u;

		internal const uint ROLE_SYSTEM_GRAPHIC = 40u;

		internal const uint ROLE_SYSTEM_STATICTEXT = 41u;

		internal const uint ROLE_SYSTEM_TEXT = 42u;

		internal const uint ROLE_SYSTEM_PUSHBUTTON = 43u;

		internal const uint ROLE_SYSTEM_CHECKBUTTON = 44u;

		internal const uint ROLE_SYSTEM_RADIOBUTTON = 45u;

		internal const uint ROLE_SYSTEM_COMBOBOX = 46u;

		internal const uint ROLE_SYSTEM_DROPLIST = 47u;

		internal const uint ROLE_SYSTEM_PROGRESSBAR = 48u;

		internal const uint ROLE_SYSTEM_DIAL = 49u;

		internal const uint ROLE_SYSTEM_HOTKEYFIELD = 50u;

		internal const uint ROLE_SYSTEM_SLIDER = 51u;

		internal const uint ROLE_SYSTEM_SPINBUTTON = 52u;

		internal const uint ROLE_SYSTEM_DIAGRAM = 53u;

		internal const uint ROLE_SYSTEM_ANIMATION = 54u;

		internal const uint ROLE_SYSTEM_EQUATION = 55u;

		internal const uint ROLE_SYSTEM_BUTTONDROPDOWN = 56u;

		internal const uint ROLE_SYSTEM_BUTTONMENU = 57u;

		internal const uint ROLE_SYSTEM_BUTTONDROPDOWNGRID = 58u;

		internal const uint ROLE_SYSTEM_WHITESPACE = 59u;

		internal const uint ROLE_SYSTEM_PAGETABLIST = 60u;

		internal const uint ROLE_SYSTEM_CLOCK = 61u;
	}

	public struct Minmaxinfo
	{
		public Point ptReserved;

		public Point ptMaxSize;

		public Point ptMaxPosition;

		public Point ptMinTrackSize;

		public Point ptMaxTrackSize;
	}

	public struct Rect
	{
		public int left;

		public int top;

		public int right;

		public int bottom;

		public static readonly Rect Empty;

		public int Width => Math.Abs(right - left);

		public int Height => bottom - top;

		public Rect(int left, int top, int right, int bottom)
		{
			this.left = left;
			this.top = top;
			this.right = right;
			this.bottom = bottom;
		}

		public Rect(Rect rcSrc)
		{
			left = rcSrc.left;
			top = rcSrc.top;
			right = rcSrc.right;
			bottom = rcSrc.bottom;
		}
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public class Monitorinfo
	{
		public int cbSize = Marshal.SizeOf(typeof(Monitorinfo));

		public Rect rcMonitor;

		public Rect rcWork;

		public int dwFlags;
	}

	internal struct Windowpos
	{
		public IntPtr hwnd;

		public IntPtr hwndInsertAfter;

		public int x;

		public int y;

		public int cx;

		public int cy;

		public int flags;
	}

	private enum SIZE
	{
		SIZE_MAXIMIZED = 2,
		SIZE_MINIMIZED = 1,
		SIZE_RESTORED = 0
	}

	internal struct DisPlayStr
	{
		internal string size;
	}

	internal struct WindowMinMax
	{
		internal double minWidth;

		internal double maxWidth;

		internal double minHeight;

		internal double maxHeight;

		internal WindowMinMax(double minSize, double maxSize)
		{
			minWidth = minSize;
			maxWidth = maxSize;
			minHeight = minSize;
			maxHeight = maxSize;
		}
	}

	private MouseButtonEventHandler DragMouseEvent;

	private Rect _drawRect;

	private MouseButtonEventHandler HeaderDoubleClick;

	private MouseButtonEventHandler RestCursorEvent;

	private Point _pressdownPoint;

	private bool _isdragPress;

	private bool _enableXResize = true;

	private bool _enableYResize = true;

	private HeaderedContentControl _header;

	private HwndSource _hs;

	private Border _borderClip;

	private System.Windows.Controls.Button _closeBtn;

	private System.Windows.Controls.Button _minBtn;

	private System.Windows.Controls.Button _maxBtn;

	private ContentControl _titlebar;

	private bool _isShowIntaskbar;

	private bool _ismodal;

	private static readonly List<Window> ActivatedWindows;

	private static object _lock;

	private bool _isResize;

	private bool _isclosing;

	private DispatcherTimer closeTimer;

	private bool isAutoClose;

	private string _minimizeTip;

	private string _maximizeTip;

	private string _restoreTip;

	private string _closeTip;

	public static readonly DependencyProperty CornerRadiusProperty;

	public static readonly DependencyProperty TitleBarProperty;

	public static readonly DependencyProperty InnerTopBorderBrushProperty;

	public static readonly DependencyProperty LeftBorderBrushProperty;

	public static readonly DependencyProperty TopBorderBrushProperty;

	public static readonly DependencyProperty RightBorderBrushProperty;

	public static readonly DependencyProperty BottomBorderBrushProperty;

	public static readonly DependencyProperty SizeAbleProperty;

	public static readonly DependencyProperty MoveAbleProperty;

	public static readonly DependencyProperty HeaderForeProperty;

	public static readonly DependencyProperty IconContentProperty;

	public static readonly DependencyProperty HeaderBrushProperty;

	public static readonly DependencyProperty CloseButtonVisibilityProperty;

	public static readonly DependencyProperty MaxButtonVisibilityProperty;

	public static readonly DependencyProperty MinButtonVisibilityProperty;

	public object customOwner;

	private bool _changebystatu;

	private bool _isclosed;

	private const int WM_SYSCOMMAND = 274;

	private const double RelativeClip = 10.0;

	private const int WM_SYSCOMMAND_SC_SIZE = 61440;

	private int cx;

	private int cy;

	private double _trackMinWidthDeviceUnits;

	private double _trackMinHeightDeviceUnits;

	private double _trackMaxWidthDeviceUnits = double.PositiveInfinity;

	private double _trackMaxHeightDeviceUnits = double.PositiveInfinity;

	private double _windowMaxWidthDeviceUnits = double.PositiveInfinity;

	private double _windowMaxHeightDeviceUnits = double.PositiveInfinity;

	public string MinimizeTip
	{
		get
		{
			return AppLocalizationService.GetLang(_minimizeTip);
		}
		set
		{
			_minimizeTip = value;
			OnPropertyChanged("MinimizeTip");
		}
	}

	public string MaximizeTip
	{
		get
		{
			return AppLocalizationService.GetLang(_maximizeTip);
		}
		set
		{
			_maximizeTip = value;
			OnPropertyChanged("MaximizeTip");
		}
	}

	public string RestoreTip
	{
		get
		{
			return AppLocalizationService.GetLang(_restoreTip);
		}
		set
		{
			_restoreTip = value;
			OnPropertyChanged("RestoreTip");
		}
	}

	public string CloseTip
	{
		get
		{
			return AppLocalizationService.GetLang("Off");
		}
		set
		{
			_closeTip = value;
			OnPropertyChanged("CloseTip");
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

	public DependencyObject TitleBar
	{
		get
		{
			return (DependencyObject)GetValue(TitleBarProperty);
		}
		set
		{
			SetValue(TitleBarProperty, value);
		}
	}

	public System.Windows.Media.Brush InnerTopBorderBrush
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(InnerTopBorderBrushProperty);
		}
		set
		{
			SetValue(InnerTopBorderBrushProperty, value);
		}
	}

	public System.Windows.Media.Brush LeftBorderBrush
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(LeftBorderBrushProperty);
		}
		set
		{
			SetValue(LeftBorderBrushProperty, value);
		}
	}

	public System.Windows.Media.Brush TopBorderBrush
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(TopBorderBrushProperty);
		}
		set
		{
			SetValue(TopBorderBrushProperty, value);
		}
	}

	public System.Windows.Media.Brush RightBorderBrush
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(RightBorderBrushProperty);
		}
		set
		{
			SetValue(RightBorderBrushProperty, value);
		}
	}

	public System.Windows.Media.Brush BottomBorderBrush
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(BottomBorderBrushProperty);
		}
		set
		{
			SetValue(BottomBorderBrushProperty, value);
		}
	}

	public bool SizeAble
	{
		get
		{
			return (bool)GetValue(SizeAbleProperty);
		}
		set
		{
			SetValue(SizeAbleProperty, value);
		}
	}

	public bool MoveAble
	{
		get
		{
			return (bool)GetValue(MoveAbleProperty);
		}
		set
		{
			SetValue(MoveAbleProperty, value);
		}
	}

	public object IconContent
	{
		get
		{
			return GetValue(IconContentProperty);
		}
		set
		{
			SetValue(IconContentProperty, value);
		}
	}

	public System.Windows.Media.Brush HeaderFore
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(HeaderForeProperty);
		}
		set
		{
			SetValue(HeaderForeProperty, value);
		}
	}

	public System.Windows.Media.Brush HeaderBrush
	{
		get
		{
			return (System.Windows.Media.Brush)GetValue(HeaderBrushProperty);
		}
		set
		{
			SetValue(HeaderBrushProperty, value);
		}
	}

	public Visibility CloseButtonVisibility
	{
		get
		{
			return (Visibility)GetValue(CloseButtonVisibilityProperty);
		}
		set
		{
			SetValue(CloseButtonVisibilityProperty, value);
		}
	}

	public Visibility MaxButtonVisibility
	{
		get
		{
			return (Visibility)GetValue(MaxButtonVisibilityProperty);
		}
		set
		{
			SetValue(MaxButtonVisibilityProperty, value);
		}
	}

	public Visibility MinButtonVisibility
	{
		get
		{
			return (Visibility)GetValue(MinButtonVisibilityProperty);
		}
		set
		{
			SetValue(MinButtonVisibilityProperty, value);
		}
	}

	public static Window LastWindow
	{
		get
		{
			lock (_lock)
			{
				return ActivatedWindows.LastOrDefault((Window p) => p.IsVisible);
			}
		}
	}

	public bool IsClosing => _isclosing;

	public new virtual bool ShowInTaskbar
	{
		get
		{
			return base.ShowInTaskbar;
		}
		set
		{
			base.ShowInTaskbar = value;
			if (!_changebystatu)
			{
				_isShowIntaskbar = value;
			}
		}
	}

	public WindowCloseReason CloseReason { get; private set; }

	public int ThreadProcessId
	{
		get
		{
			if (_hs != null)
			{
				return _hs.Handle.GetWindowThreadProcessId();
			}
			return 0;
		}
	}

	internal Rect WindowBounds
	{
		[SecurityCritical]
		[SecurityTreatAsSafe]
		get
		{
			Rect lpRect = new Rect(0, 0, 0, 0);
			GetWindowRect(CriticalHandle, ref lpRect);
			return lpRect;
		}
	}

	private Rect ClientBounds
	{
		[SecurityCritical]
		[SecurityTreatAsSafe]
		get
		{
			_GetClientRect(CriticalHandle, out var lpRect);
			return lpRect;
		}
	}

	internal IntPtr CriticalHandle
	{
		[SecurityCritical]
		get
		{
			if (_hs != null)
			{
				return _hs.Handle;
			}
			return IntPtr.Zero;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	static BaseWindow()
	{
		_lock = new object();
		CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(BaseWindow), new PropertyMetadata(new CornerRadius(0.0)));
		TitleBarProperty = DependencyProperty.Register("TitleBar", typeof(DependencyObject), typeof(BaseWindow), new PropertyMetadata(null));
		InnerTopBorderBrushProperty = DependencyProperty.Register("InnerTopBorderBrush", typeof(System.Windows.Media.Brush), typeof(BaseWindow), new PropertyMetadata((object)null));
		LeftBorderBrushProperty = DependencyProperty.Register("LeftBorderBrush", typeof(System.Windows.Media.Brush), typeof(BaseWindow), new PropertyMetadata(null));
		TopBorderBrushProperty = DependencyProperty.Register("TopBorderBrush", typeof(System.Windows.Media.Brush), typeof(BaseWindow), new PropertyMetadata(null));
		RightBorderBrushProperty = DependencyProperty.Register("RightBorderBrush", typeof(System.Windows.Media.Brush), typeof(BaseWindow), new PropertyMetadata(null));
		BottomBorderBrushProperty = DependencyProperty.Register("BottomBorderBrush", typeof(System.Windows.Media.Brush), typeof(BaseWindow), new PropertyMetadata(null));
		SizeAbleProperty = DependencyProperty.Register("SizeAble", typeof(bool), typeof(BaseWindow), new PropertyMetadata(false));
		MoveAbleProperty = DependencyProperty.Register("MoveAble", typeof(bool), typeof(BaseWindow), new PropertyMetadata(true));
		HeaderForeProperty = DependencyProperty.Register("HeaderFore", typeof(System.Windows.Media.Brush), typeof(BaseWindow), new PropertyMetadata(new SolidColorBrush(Colors.White)));
		IconContentProperty = DependencyProperty.Register("IconContent", typeof(object), typeof(BaseWindow), new PropertyMetadata(null));
		HeaderBrushProperty = DependencyProperty.Register("HeaderBrush", typeof(System.Windows.Media.Brush), typeof(BaseWindow), new PropertyMetadata(null));
		CloseButtonVisibilityProperty = DependencyProperty.Register("CloseButtonVisibility", typeof(Visibility), typeof(BaseWindow), new FrameworkPropertyMetadata(Visibility.Visible), null);
		MaxButtonVisibilityProperty = DependencyProperty.Register("MaxButtonVisibility", typeof(Visibility), typeof(BaseWindow), new PropertyMetadata(Visibility.Visible));
		MinButtonVisibilityProperty = DependencyProperty.Register("MinButtonVisibility", typeof(Visibility), typeof(BaseWindow), new FrameworkPropertyMetadata(Visibility.Visible), null);
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(BaseWindow), new FrameworkPropertyMetadata(typeof(BaseWindow)));
		lock (_lock)
		{
			ActivatedWindows = new List<Window>();
		}
	}

	public BaseWindow()
	{
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		SourceInitialized += WsInitialized;
		MaximizeTip = "最大化";
		MinimizeTip = "最小化";
		RestoreTip = "向下还原";
		CloseTip = "关闭";
		CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, CloseWindow));
		CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, MaximizeWindow, CanResizeWindow));
		CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, MinimizeWindow, CanMinimizeWindow));
		CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, RestoreWindow, CanResizeWindow));
	}

	private void CanResizeWindow(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;
	}

	private void CanMinimizeWindow(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ResizeMode != ResizeMode.NoResize;
	}

	private void CloseWindow(object sender, ExecutedRoutedEventArgs e)
	{
		Close();
	}

	private void MaximizeWindow(object sender, ExecutedRoutedEventArgs e)
	{
		SystemCommands.MaximizeWindow(this);
	}

	private void MinimizeWindow(object sender, ExecutedRoutedEventArgs e)
	{
		SystemCommands.MinimizeWindow(this);
	}

	private void RestoreWindow(object sender, ExecutedRoutedEventArgs e)
	{
		SystemCommands.RestoreWindow(this);
	}

	public void OnPropertyChanged(string propname)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propname));
		}
	}

	public void SetPropertyChanged<T>(ref T prop, T value, [CallerMemberName] string propname = null)
	{
		if (!EqualityComparer<T>.Default.Equals(prop, value))
		{
			prop = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
		}
	}

	private void ResetCursor(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (!SizeAble || Mouse.LeftButton != MouseButtonState.Pressed)
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
		}
	}

	private void DisplayResizeCursor()
	{
		if (!SizeAble)
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
			return;
		}
		System.Windows.Point position = Mouse.GetPosition(this);
		double x = position.X;
		double y = position.Y;
		double actualWidth = ActualWidth;
		double actualHeight = ActualHeight;
		if ((x >= actualWidth - 10.0) & (y >= actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeNWSE;
		}
		else if ((x <= 10.0) & (y >= actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeNESW;
		}
		else if ((x >= actualWidth - 10.0) & (y >= 10.0 + _header.ActualHeight) & (y <= actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeWE;
		}
		else if ((x >= 10.0) & (x <= actualWidth - 10.0) & (y > actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeNS;
		}
		else if ((x <= 10.0) & (y <= actualHeight - 10.0) & (y >= 10.0 + _header.ActualHeight))
		{
			Cursor = System.Windows.Input.Cursors.SizeWE;
		}
		else
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
		}
	}

	private static System.Windows.Input.Cursor GetCurrentCursor()
	{
		System.Windows.Forms.Cursor current = System.Windows.Forms.Cursor.Current;
		if (current == System.Windows.Forms.Cursors.Arrow)
		{
			return System.Windows.Input.Cursors.Arrow;
		}
		if (current == System.Windows.Forms.Cursors.AppStarting)
		{
			return System.Windows.Input.Cursors.AppStarting;
		}
		if (current == System.Windows.Forms.Cursors.Cross)
		{
			return System.Windows.Input.Cursors.Cross;
		}
		if (current == System.Windows.Forms.Cursors.Hand)
		{
			return System.Windows.Input.Cursors.Hand;
		}
		if (current == System.Windows.Forms.Cursors.Help)
		{
			return System.Windows.Input.Cursors.Help;
		}
		if (current == System.Windows.Forms.Cursors.IBeam)
		{
			return System.Windows.Input.Cursors.IBeam;
		}
		if (current == System.Windows.Forms.Cursors.No)
		{
			return System.Windows.Input.Cursors.No;
		}
		if (current == System.Windows.Forms.Cursors.SizeAll)
		{
			return System.Windows.Input.Cursors.SizeAll;
		}
		if (current == System.Windows.Forms.Cursors.SizeNESW)
		{
			return System.Windows.Input.Cursors.SizeNESW;
		}
		if (current == System.Windows.Forms.Cursors.SizeNS)
		{
			return System.Windows.Input.Cursors.SizeNS;
		}
		if (current == System.Windows.Forms.Cursors.SizeNWSE)
		{
			return System.Windows.Input.Cursors.SizeNWSE;
		}
		if (current == System.Windows.Forms.Cursors.SizeWE)
		{
			return System.Windows.Input.Cursors.SizeWE;
		}
		return null;
	}

	private void ResizeWindow(ResizeDirection direction)
	{
		if (_hs != null)
		{
			_isResize = true;
			IntPtr wParam = (IntPtr)(int)(61440 + direction);
			SendMessage(_hs.Handle, 274u, wParam, IntPtr.Zero);
		}
	}

	private void Resize()
	{
		if (!SizeAble)
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
			return;
		}
		if (GetCurrentCursor() != Cursor)
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
			return;
		}
		System.Windows.Point position = Mouse.GetPosition(this);
		double x = position.X;
		double y = position.Y;
		double actualWidth = ActualWidth;
		double actualHeight = ActualHeight;
		if (x > actualWidth)
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
			return;
		}
		if (y > actualHeight)
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
			return;
		}
		if ((x >= actualWidth - 10.0) & (y >= actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeNWSE;
			ResizeWindow(ResizeDirection.BottomRight);
		}
		else if ((x <= 10.0) & (y >= actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeNESW;
			ResizeWindow(ResizeDirection.BottomLeft);
		}
		else if ((x >= actualWidth - 10.0) & (y >= 10.0 + _header.ActualHeight) & (y <= actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeWE;
			ResizeWindow(ResizeDirection.Right);
		}
		else if ((x >= 10.0) & (x <= actualWidth - 10.0) & (y > actualHeight - 10.0))
		{
			Cursor = System.Windows.Input.Cursors.SizeNS;
			ResizeWindow(ResizeDirection.Bottom);
		}
		else if (!((x <= 10.0) & (y <= actualHeight - 10.0) & (y >= 10.0 + _header.ActualHeight)))
		{
			Cursor = System.Windows.Input.Cursors.Arrow;
		}
		else
		{
			Cursor = System.Windows.Input.Cursors.SizeWE;
			ResizeWindow(ResizeDirection.Left);
		}
		Cursor = System.Windows.Input.Cursors.Arrow;
	}

	private void WsInitialized(object sender, EventArgs e)
	{
		_hs = PresentationSource.FromVisual(this) as HwndSource;
		if (_hs != null)
		{
			_hs.AddHook(WindowProc);
		}
	}

	public new virtual bool? ShowDialog()
	{
		if (_isclosing)
		{
			return false;
		}
		_ismodal = true;
		CloseReason = WindowCloseReason.None;
		if (Owner != null || System.Windows.Application.Current == null || System.Windows.Application.Current.Dispatcher.Thread != Dispatcher.Thread)
		{
			if (customOwner is DependencyObject dependencyObject && PresentationSource.FromDependencyObject(dependencyObject) is HwndSource hwndSource)
			{
				new WindowInteropHelper(this).Owner = hwndSource.Handle;
			}
			return base.ShowDialog();
		}
		Window window = System.Windows.Application.Current.MainWindow;
		Window lastWindow = LastWindow;
		if (lastWindow != null)
		{
			window = lastWindow;
		}
		if (window != null && window.IsVisible)
		{
			Owner = window;
		}
		if (Owner != null && Owner.Dispatcher.Thread != Dispatcher.Thread)
		{
			Owner = null;
		}
		return base.ShowDialog();
	}

	public bool? ShowDialogDelay(uint time)
	{
		if (_isclosing)
		{
			return false;
		}
		_ismodal = true;
		CloseReason = WindowCloseReason.None;
		if (closeTimer == null)
		{
			closeTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(time)
			};
		}
		else
		{
			closeTimer.Interval = TimeSpan.FromMilliseconds(time);
		}
		closeTimer.Tick -= CloseTimer_Elapsed;
		closeTimer.Tick += CloseTimer_Elapsed;
		isAutoClose = true;
		if (Owner != null || System.Windows.Application.Current == null || System.Windows.Application.Current.Dispatcher.Thread != Dispatcher.Thread)
		{
			closeTimer.Start();
			return base.ShowDialog();
		}
		Window window = System.Windows.Application.Current.MainWindow;
		Window lastWindow = LastWindow;
		if (lastWindow != null && !(lastWindow is BaseWindow { isAutoClose: not false }))
		{
			window = lastWindow;
		}
		if (window != null && window.IsVisible)
		{
			Owner = window;
		}
		if (Owner != null && Owner.Dispatcher.Thread != Dispatcher.Thread)
		{
			Owner = null;
		}
		closeTimer.Start();
		return base.ShowDialog();
	}

	private void CloseTimer_Elapsed(object sender, EventArgs e)
	{
		try
		{
			if (!IsClosing)
			{
				Close();
			}
		}
		finally
		{
			closeTimer?.Stop();
		}
	}

	public new virtual void Show()
	{
		if (_isclosing)
		{
			return;
		}
		CloseReason = WindowCloseReason.None;
		if (_ismodal)
		{
			ShowDialog();
			return;
		}
		if (Owner == null)
		{
			ShowInTaskbar = true;
		}
		base.Show();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		ColorThemesResourceDictionary.GetInstance(this);
		ControlTemplate template = Template;
		_closeBtn = (System.Windows.Controls.Button)template.FindName("CloseWinButton", this);
		_minBtn = (System.Windows.Controls.Button)template.FindName("MinWinButton", this);
		_maxBtn = (System.Windows.Controls.Button)template.FindName("MaxWinButton", this);
		_header = (HeaderedContentControl)template.FindName("Header", this);
		_titlebar = (ContentControl)template.FindName("titlebar", this);
		_isShowIntaskbar = ShowInTaskbar;
		StateChanged += BaseWindow_StateChanged;
		HeaderDoubleClick = Header_DoubleClick;
		_header.AddHandler(System.Windows.Controls.Control.MouseDoubleClickEvent, HeaderDoubleClick);
		DragMouseEvent = (object s1, MouseButtonEventArgs e1) =>
		{
			if (e1.ChangedButton == MouseButton.Left && e1.LeftButton != MouseButtonState.Released && WindowState == WindowState.Normal && MoveAble)
			{
				DragMove();
			}
		};
		_header.AddHandler(UIElement.MouseLeftButtonDownEvent, DragMouseEvent);
		_titlebar.AddHandler(UIElement.MouseLeftButtonDownEvent, DragMouseEvent);
		_header.AddHandler(UIElement.MouseLeftButtonUpEvent, DragMouseEvent);
		_titlebar.AddHandler(UIElement.MouseLeftButtonUpEvent, DragMouseEvent);
		PreviewMouseMove += ResetCursor;
		_borderClip = (Border)template.FindName("Border", this);
		RestCursorEvent = ResetCursor;
		_borderClip.AddHandler(UIElement.MouseUpEvent, RestCursorEvent);
		_borderClip.MouseMove += _borderClip_MouseMove;
		_borderClip.PreviewMouseDown += _borderClip_PreviewMouseDown;
		SizeChanged += BaseWindow_SizeChanged;
		_closeBtn.Click += closeBtn_Click;
		_minBtn.Click += _minBtn_Click;
		_maxBtn.Click += _maxBtn_Click;
		Loaded += (object s, RoutedEventArgs e) =>
		{
			double actualHeight = _header.ActualHeight;
			double val = _closeBtn.ActualWidth + _minBtn.ActualWidth + _maxBtn.ActualWidth + 30.0;
			MinHeight = Math.Max(actualHeight, MinHeight);
			MinWidth = Math.Max(val, MinWidth);
			OnLoaded();
		};
	}

	protected virtual void Header_DoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (!(sender is UIElement uIElement))
		{
			return;
		}
		System.Windows.Point position = e.GetPosition(uIElement);
		if (!(position.X < 0.0) && !(position.Y < 0.0) && !(position.X > uIElement.RenderSize.Width) && !(position.Y > uIElement.RenderSize.Height) && MaxButtonVisibility == Visibility.Visible)
		{
			switch (WindowState)
			{
			case WindowState.Maximized:
				WindowState = WindowState.Normal;
				break;
			case WindowState.Normal:
				WindowState = WindowState.Maximized;
				break;
			}
		}
	}

	protected void SetHeaderIsHitTestVisibleInChrome(bool status)
	{
		WindowChrome.SetIsHitTestVisibleInChrome(_header, status);
	}

	private void _maxBtn_Click(object sender, RoutedEventArgs e)
	{
		WindowState = ((WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void _minBtn_Click(object sender, RoutedEventArgs e)
	{
		WindowState = WindowState.Minimized;
	}

	private void _borderClip_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		DisplayResizeCursor();
	}

	private void BaseWindow_StateChanged(object sender, EventArgs e)
	{
		_changebystatu = true;
		if (WindowState == WindowState.Minimized)
		{
			_isShowIntaskbar = ShowInTaskbar;
			ShowInTaskbar = true;
		}
		else
		{
			ShowInTaskbar = _isShowIntaskbar;
		}
		_changebystatu = false;
	}

	private void BaseWindow_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		TextBlock textBlock = (TextBlock)Template.FindName("Title", this);
		if (e.WidthChanged)
		{
			double num = Width - (_closeBtn.ActualWidth + _minBtn.ActualWidth + _maxBtn.ActualWidth + 30.0);
			if (textBlock != null)
			{
				textBlock.MaxWidth = ((num > 0.0) ? num : 0.0);
			}
		}
		if (_isResize)
		{
			if (WindowState == WindowState.Maximized)
			{
				WindowState = WindowState.Normal;
			}
			_isResize = false;
		}
	}

	private void _borderClip_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		Resize();
	}

	private void DrawMoveRect(Rect rect)
	{
		ControlPaint.DrawReversibleFrame(new Rectangle(new System.Drawing.Point(rect.left, rect.top), new System.Drawing.Size(rect.Width, rect.Height)), System.Drawing.Color.Black, FrameStyle.Thick);
	}

	protected virtual void OnLoaded()
	{
		if (IsVisible)
		{
			bool topmost = Topmost;
			if (!topmost)
			{
				Topmost = true;
			}
			Activate();
			Topmost = topmost;
		}
	}

	protected override void OnActivated(EventArgs e)
	{
		base.OnActivated(e);
		lock (_lock)
		{
			if (ActivatedWindows.Contains(this))
			{
				ActivatedWindows.Remove(this);
			}
			ActivatedWindows.Add(this);
		}
	}

	private void closeBtn_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	public new virtual void Close()
	{
		if (_isclosed)
		{
			return;
		}
		CloseReason = WindowCloseReason.UserClosing;
		try
		{
			base.Close();
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
		}
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		_enableXResize = true;
		_enableYResize = true;
		double actualWidth = ActualWidth;
		double actualHeight = ActualHeight;
		if (actualWidth < MinWidth)
		{
			_enableXResize = false;
		}
		if (actualHeight < MinHeight)
		{
			_enableYResize = false;
		}
		if (!_enableXResize)
		{
			Width = MinWidth;
		}
		else if (!_enableYResize)
		{
			Height = MinHeight;
		}
		else
		{
			base.OnRender(drawingContext);
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		if (!IsClosing)
		{
			_isclosing = true;
		}
		lock (_lock)
		{
			if (ActivatedWindows.Contains(this))
			{
				ActivatedWindows.Remove(this);
			}
		}
		base.OnClosed(e);
		_isclosed = true;
		if (_header != null)
		{
			_header.RemoveHandler(UIElement.MouseLeftButtonDownEvent, DragMouseEvent);
			_header.RemoveHandler(UIElement.MouseLeftButtonUpEvent, DragMouseEvent);
			_header.RemoveHandler(System.Windows.Controls.Control.MouseDoubleClickEvent, HeaderDoubleClick);
		}
		if (_titlebar != null)
		{
			_titlebar.RemoveHandler(UIElement.MouseLeftButtonDownEvent, DragMouseEvent);
			_titlebar.RemoveHandler(UIElement.MouseLeftButtonUpEvent, DragMouseEvent);
		}
		if (_borderClip != null)
		{
			_borderClip.MouseMove -= _borderClip_MouseMove;
			_borderClip.PreviewMouseDown -= _borderClip_PreviewMouseDown;
			_borderClip.RemoveHandler(UIElement.MouseUpEvent, RestCursorEvent);
		}
		WindowInteropHelper windowInteropHelper = new WindowInteropHelper(this);
		_ = windowInteropHelper.Owner;
		if (windowInteropHelper.Owner != IntPtr.Zero)
		{
			SetForegroundWindow(windowInteropHelper.Owner);
		}
		if (_hs != null)
		{
			_hs.RemoveHook(WindowProc);
			_hs.Dispose();
			_hs = null;
			if (Owner != null)
			{
				Owner = null;
			}
			closeTimer?.Stop();
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		if (e.Cancel)
		{
			return;
		}
		_isclosing = true;
		Window[] array = OwnedWindows.OfType<Window>().ToArray();
		foreach (Window window in array)
		{
			if (window.IsLoaded && window.IsVisible)
			{
				window.Hide();
			}
		}
		Owner?.Activate();
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr MonitorFromPoint(Point pt, MonitorOptions dwFlags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(IntPtr hWnd, ref Rect lpRect);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, MonitorOptions dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32")]
	internal static extern bool GetMonitorInfo(IntPtr hMonitor, Monitorinfo lpmi);

	[DllImport("User32")]
	internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out Point lpPoint);

	private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		switch (msg)
		{
		case 24:
		{
			Rect lpRect = default;
			if (!GetWindowRect(hwnd, ref lpRect))
			{
				break;
			}
			Point pt = new Point(lpRect.left, lpRect.top);
			IntPtr hMonitor = MonitorFromPoint(pt, MonitorOptions.MONITOR_DEFAULTTONEAREST);
			Monitorinfo monitorinfo = new Monitorinfo();
			bool flag = false;
			if (GetMonitorInfo(hMonitor, monitorinfo))
			{
				if (pt.x < monitorinfo.rcWork.left)
				{
					pt.x = monitorinfo.rcWork.left;
					flag = true;
				}
				if (pt.y < monitorinfo.rcWork.top)
				{
					pt.y = monitorinfo.rcWork.top;
					if (!flag)
					{
						flag = true;
					}
				}
			}
			if (flag)
			{
				WindowHelper.SetWindowPos(hwnd, 0, pt.x, pt.y, 0, 0, 1u);
			}
			break;
		}
		case 126:
			WmDispalyChange(hwnd);
			handled = true;
			break;
		case 36:
			WmGetMinMaxInfo(lParam, hwnd);
			handled = true;
			break;
		case 70:
		{
			Windowpos windowpos = (Windowpos)Marshal.PtrToStructure(lParam, typeof(Windowpos));
			if (((ulong)windowpos.flags & 2uL) == 0L)
			{
				HwndSource hwndSource = HwndSource.FromHwnd(hwnd);
				if (hwndSource != null && !(hwndSource.RootVisual is Window))
				{
					return IntPtr.Zero;
				}
				if ((double)windowpos.cx < MinWidth)
				{
					windowpos.cx = (int)MinWidth;
				}
				if ((double)windowpos.cy < MinHeight)
				{
					windowpos.cy = (int)MinHeight;
				}
			}
			if (WindowState == WindowState.Normal)
			{
				cy = windowpos.cy;
				cx = windowpos.cx;
			}
			Marshal.StructureToPtr((object)windowpos, lParam, true);
			handled = true;
			break;
		}
		case 5:
		{
			WINDOWPLACEMENT placement = new WINDOWPLACEMENT
			{
				length = Marshal.SizeOf(typeof(WINDOWPLACEMENT))
			};
			WindowHelper.HWND hWND = WindowHelper.HWND.Cast(hwnd);
			if (!WindowHelper.GetWindowPlacement(hWND, ref placement))
			{
				int num = placement.rcNormalPosition_right - placement.rcNormalPosition_left;
				int num2 = placement.rcNormalPosition_bottom - placement.rcNormalPosition_top;
				if ((double)num < 0.0 || (double)num2 < 0.0)
				{
					WINDOWPLACEMENT wp = placement;
					wp.rcNormalPosition_right = wp.rcNormalPosition_left + cx;
					wp.rcNormalPosition_bottom = wp.rcNormalPosition_top + cy;
					WindowHelper.SetWindowPlacement(hWND, ref wp);
				}
				else
				{
					cx = num;
					cy = num2;
				}
			}
			break;
		}
		case 17:
		case 22:
			CloseReason = WindowCloseReason.WindowsShutDown;
			break;
		case 274:
			if ((LOWORD(wParam) & 0xFFF0) == 61536)
			{
				CloseReason = WindowCloseReason.UserClosing;
			}
			break;
		case 16:
			if (CloseReason == WindowCloseReason.None)
			{
				CloseReason = WindowCloseReason.TaskManagerClosing;
			}
			break;
		}
		return (IntPtr)0;
	}

	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	private static int LOWORD(IntPtr n)
	{
		return LOWORD((int)(long)n);
	}

	private static int LOWORD(int n)
	{
		return n & 0xFFFF;
	}

	private void UpdateHwndSizeOnWidthHeightChange(double widthLogicalUnits, double heightLogicalUnits)
	{
		System.Windows.Point point = LogicalToDeviceUnits(new System.Windows.Point(widthLogicalUnits, heightLogicalUnits));
		WindowHelper.SetWindowPos(_hs.Handle, 0, 0, 0, DoubleUtil.DoubleToInt(point.X), DoubleUtil.DoubleToInt(point.Y), 22u);
	}

	private void WmDispalyChange(IntPtr winPtr)
	{
		if (!SizeAble || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA || Thread.CurrentThread.ManagedThreadId != 1 || Thread.CurrentThread.IsBackground || System.Windows.Application.Current == null || System.Windows.Application.Current.MainWindow.WindowState != WindowState.Maximized)
		{
			return;
		}
		IntPtr hMonitor = MonitorFromPoint(new Point(0, 0), MonitorOptions.MONITOR_DEFAULTTOPRIMARY);
		Monitorinfo monitorinfo = new Monitorinfo();
		if (GetMonitorInfo(hMonitor, monitorinfo))
		{
			IntPtr intPtr = MonitorFromWindow(winPtr, MonitorOptions.MONITOR_DEFAULTTONEAREST);
			if (hMonitor.Equals(intPtr))
			{
				WindowHelper.SetWindowPos(winPtr, 0, monitorinfo.rcWork.left, monitorinfo.rcWork.top, monitorinfo.rcWork.right - monitorinfo.rcWork.left, monitorinfo.rcWork.bottom - monitorinfo.rcWork.top, 32u);
				return;
			}
			WindowHelper.SetWindowPos(winPtr, 0, monitorinfo.rcMonitor.left, monitorinfo.rcMonitor.top, monitorinfo.rcMonitor.right - monitorinfo.rcMonitor.left, monitorinfo.rcMonitor.bottom - monitorinfo.rcMonitor.top, 4u);
			UpdateResizeWindow();
		}
	}

	private void UpdateResizeWindow()
	{
		System.Windows.Application.Current.MainWindow.Width = 1366.0;
		System.Windows.Application.Current.MainWindow.Height = 768.0;
		System.Windows.Application.Current.MainWindow.WindowState = WindowState.Normal;
		System.Windows.Application.Current.MainWindow.WindowState = WindowState.Maximized;
	}

	private void WmGetMinMaxInfo(IntPtr lParam, IntPtr winPtr)
	{
		IntPtr hMonitor = MonitorFromPoint(new Point(0, 0), MonitorOptions.MONITOR_DEFAULTTOPRIMARY);
		Monitorinfo monitorinfo = new Monitorinfo();
		if (!GetMonitorInfo(hMonitor, monitorinfo))
		{
			return;
		}
		Minmaxinfo minmaxinfo = (Minmaxinfo)Marshal.PtrToStructure(lParam, typeof(Minmaxinfo));
		IntPtr intPtr = MonitorFromWindow(winPtr, MonitorOptions.MONITOR_DEFAULTTONEAREST);
		if (hMonitor.Equals(intPtr))
		{
			minmaxinfo.ptMaxPosition.x = monitorinfo.rcWork.left;
			minmaxinfo.ptMaxPosition.y = monitorinfo.rcWork.top;
			minmaxinfo.ptMaxSize.x = monitorinfo.rcWork.right - monitorinfo.rcWork.left;
			minmaxinfo.ptMaxSize.y = monitorinfo.rcWork.bottom - monitorinfo.rcWork.top;
		}
		else
		{
			Monitorinfo monitorinfo2 = new Monitorinfo();
			if (!GetMonitorInfo(intPtr, monitorinfo2))
			{
				return;
			}
			minmaxinfo.ptMaxPosition.x = monitorinfo.rcMonitor.left;
			minmaxinfo.ptMaxPosition.y = monitorinfo.rcMonitor.top;
			minmaxinfo.ptMaxSize.x = monitorinfo2.rcWork.right - monitorinfo2.rcWork.left;
			minmaxinfo.ptMaxSize.y = monitorinfo2.rcWork.bottom - monitorinfo2.rcWork.top;
			minmaxinfo.ptMaxTrackSize.x = monitorinfo2.rcWork.right - monitorinfo2.rcWork.left;
			minmaxinfo.ptMaxTrackSize.y = monitorinfo2.rcWork.bottom - monitorinfo2.rcWork.top;
		}
		_trackMinWidthDeviceUnits = minmaxinfo.ptMinTrackSize.x;
		_trackMinHeightDeviceUnits = minmaxinfo.ptMinTrackSize.y;
		_trackMaxWidthDeviceUnits = minmaxinfo.ptMaxTrackSize.x;
		_trackMaxHeightDeviceUnits = minmaxinfo.ptMaxTrackSize.y;
		_windowMaxWidthDeviceUnits = minmaxinfo.ptMaxSize.x;
		_windowMaxHeightDeviceUnits = minmaxinfo.ptMaxSize.y;
		if (_hs != null && _hs.CompositionTarget != null)
		{
			WindowMinMax windowMinMax = GetWindowMinMax();
			System.Windows.Point point = LogicalToDeviceUnits(new System.Windows.Point(windowMinMax.minWidth, windowMinMax.minHeight));
			System.Windows.Point point2 = LogicalToDeviceUnits(new System.Windows.Point(windowMinMax.maxWidth, windowMinMax.maxHeight));
			minmaxinfo.ptMinTrackSize.x = DoubleUtil.DoubleToInt(point.X);
			minmaxinfo.ptMinTrackSize.y = DoubleUtil.DoubleToInt(point.Y);
			minmaxinfo.ptMaxTrackSize.x = DoubleUtil.DoubleToInt(point2.X);
			minmaxinfo.ptMaxTrackSize.y = DoubleUtil.DoubleToInt(point2.Y);
		}
		Marshal.StructureToPtr((object)minmaxinfo, lParam, true);
	}

	internal System.Windows.Point DeviceToLogicalUnits(System.Windows.Point ptDeviceUnits)
	{
		return _hs.CompositionTarget.TransformFromDevice.Transform(ptDeviceUnits);
	}

	[SecurityCritical]
	[SecurityTreatAsSafe]
	internal System.Windows.Point LogicalToDeviceUnits(System.Windows.Point ptLogicalUnits)
	{
		return _hs.CompositionTarget.TransformToDevice.Transform(ptLogicalUnits);
	}

	private System.Windows.Size MeasureOverrideHelper(System.Windows.Size constraint)
	{
		if (_hs == null || _hs.CompositionTarget == null)
		{
			return new System.Windows.Size(0.0, 0.0);
		}
		if (VisualChildrenCount > 0 && GetVisualChild(0) is UIElement uIElement)
		{
			System.Windows.Size hwndNonClientAreaSizeInMeasureUnits = GetHwndNonClientAreaSizeInMeasureUnits();
			uIElement.Measure(new System.Windows.Size
			{
				Width = ((constraint.Width == double.PositiveInfinity) ? double.PositiveInfinity : Math.Max(0.0, constraint.Width - hwndNonClientAreaSizeInMeasureUnits.Width)),
				Height = ((constraint.Height == double.PositiveInfinity) ? double.PositiveInfinity : Math.Max(0.0, constraint.Height - hwndNonClientAreaSizeInMeasureUnits.Height))
			});
			System.Windows.Size desiredSize = uIElement.DesiredSize;
			return new System.Windows.Size(desiredSize.Width + hwndNonClientAreaSizeInMeasureUnits.Width, desiredSize.Height + hwndNonClientAreaSizeInMeasureUnits.Height);
		}
		return GetSizeFromHwndInMeasureUnits();
	}

	protected override System.Windows.Size ArrangeOverride(System.Windows.Size arrangeBounds)
	{
		if (_hs == null || _hs.CompositionTarget == null)
		{
			return arrangeBounds;
		}
		WindowMinMax windowMinMax = GetWindowMinMax();
		arrangeBounds.Width = Math.Max(windowMinMax.minWidth, Math.Min(arrangeBounds.Width, windowMinMax.maxWidth));
		arrangeBounds.Height = Math.Max(windowMinMax.minHeight, Math.Min(arrangeBounds.Height, windowMinMax.maxHeight));
		if (VisualChildrenCount > 0 && GetVisualChild(0) is UIElement uIElement)
		{
			System.Windows.Size hwndNonClientAreaSizeInMeasureUnits = GetHwndNonClientAreaSizeInMeasureUnits();
			uIElement.Arrange(new System.Windows.Rect(new System.Windows.Size
			{
				Width = Math.Max(0.0, arrangeBounds.Width - hwndNonClientAreaSizeInMeasureUnits.Width),
				Height = Math.Max(0.0, arrangeBounds.Height - hwndNonClientAreaSizeInMeasureUnits.Height)
			}));
		}
		return arrangeBounds;
	}

	[SecurityCritical]
	[SecurityTreatAsSafe]
	internal System.Windows.Size GetSizeFromHwndInMeasureUnits()
	{
		System.Windows.Point point = new System.Windows.Point(0.0, 0.0);
		Rect windowBounds = WindowBounds;
		point.X = windowBounds.right - windowBounds.left;
		point.Y = windowBounds.bottom - windowBounds.top;
		point = _hs.CompositionTarget.TransformFromDevice.Transform(point);
		return new System.Windows.Size(point.X, point.Y);
	}

	[SecurityCritical]
	[SecurityTreatAsSafe]
	internal System.Windows.Size GetHwndNonClientAreaSizeInMeasureUnits()
	{
		Rect clientBounds = ClientBounds;
		Rect windowBounds = WindowBounds;
		System.Windows.Point point = new System.Windows.Point(windowBounds.right - windowBounds.left - (clientBounds.right - clientBounds.left), windowBounds.bottom - windowBounds.top - (clientBounds.bottom - clientBounds.top));
		if (_hs != null && _hs.CompositionTarget != null)
		{
			point = _hs.CompositionTarget.TransformFromDevice.Transform(point);
		}
		return new System.Windows.Size(Math.Max(0.0, point.X), Math.Max(0.0, point.Y));
	}

	[SecurityCritical]
	public static Rect GetClientRect(IntPtr hwnd)
	{
		_GetClientRect(hwnd, out var lpRect);
		return lpRect;
	}

	[DllImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
	[SecurityCritical]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool _GetClientRect(IntPtr hwnd, out Rect lpRect);

	protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
	{
		if (_hs == null || _hs.CompositionTarget == null)
		{
			return base.MeasureOverride(availableSize);
		}
		System.Windows.Size constraint = new System.Windows.Size(availableSize.Width, availableSize.Height);
		WindowMinMax windowMinMax = GetWindowMinMax();
		constraint.Width = Math.Max(windowMinMax.minWidth, Math.Min(constraint.Width, windowMinMax.maxWidth));
		constraint.Height = Math.Max(windowMinMax.minHeight, Math.Min(constraint.Height, windowMinMax.maxHeight));
		System.Windows.Size size = MeasureOverrideHelper(constraint);
		return new System.Windows.Size(Math.Max(size.Width, windowMinMax.minWidth), Math.Max(size.Height, windowMinMax.minHeight));
	}

	internal virtual WindowMinMax GetWindowMinMax()
	{
		WindowMinMax result = default;
		double x = _trackMaxWidthDeviceUnits;
		double y = _trackMaxHeightDeviceUnits;
		if (WindowState == WindowState.Maximized)
		{
			x = Math.Max(_trackMaxWidthDeviceUnits, _windowMaxWidthDeviceUnits);
			y = Math.Max(_trackMaxHeightDeviceUnits, _windowMaxHeightDeviceUnits);
		}
		System.Windows.Point point = DeviceToLogicalUnits(new System.Windows.Point(x, y));
		System.Windows.Point point2 = DeviceToLogicalUnits(new System.Windows.Point(_trackMinWidthDeviceUnits, _trackMinHeightDeviceUnits));
		result.minWidth = Math.Max(MinWidth, point2.X);
		if (MinWidth > MaxWidth)
		{
			result.maxWidth = Math.Min(MinWidth, point.X);
		}
		else if (!double.IsPositiveInfinity(MaxWidth))
		{
			result.maxWidth = Math.Min(MaxWidth, point.X);
		}
		else
		{
			result.maxWidth = point.X;
		}
		result.minHeight = Math.Max(MinHeight, point2.Y);
		if (MinHeight > MaxHeight)
		{
			result.maxHeight = Math.Min(MinHeight, point.Y);
		}
		else if (!double.IsPositiveInfinity(MaxHeight))
		{
			result.maxHeight = Math.Min(MaxHeight, point.Y);
		}
		else
		{
			result.maxHeight = point.Y;
		}
		return result;
	}
}
