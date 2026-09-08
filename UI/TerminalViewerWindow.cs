using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AS400PADCustomAction.UI
{
    /// <summary>
    /// Pure C# WPF floating window rendering a real-time IBM 5250 green-screen terminal emulator.
    /// Non-blocking, high-DPI aware, and runs on an independent STA thread.
    /// </summary>
    public class TerminalViewerWindow : Window
    {
        private readonly string _sessionId;
        private TextBlock _screenTextBlock;
        private TextBlock _statusSessionTextBlock;
        private TextBlock _statusConnTextBlock;
        private TextBlock _statusCursorTextBlock;
        private TextBlock _statusActionTextBlock;
        private CheckBox _alwaysOnTopCheckBox;

        private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(10, 10, 10));
        private static readonly Brush TerminalBgBrush = new SolidColorBrush(Color.FromRgb(5, 5, 5));
        private static readonly Brush GreenTextBrush = new SolidColorBrush(Color.FromRgb(0, 255, 102)); // Vivid phosphor emerald
        private static readonly Brush HeaderBgBrush = new SolidColorBrush(Color.FromRgb(22, 24, 26));
        private static readonly Brush FooterBgBrush = new SolidColorBrush(Color.FromRgb(18, 20, 22));
        private static readonly Brush BorderSubtleBrush = new SolidColorBrush(Color.FromRgb(35, 45, 40));
        private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
        private static readonly Brush TextDimBrush = new SolidColorBrush(Color.FromRgb(110, 110, 110));
        private static readonly Brush ConnOnlineBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        private static readonly Brush ConnOfflineBrush = new SolidColorBrush(Color.FromRgb(255, 82, 82));

        public TerminalViewerWindow(string sessionId, bool alwaysOnTop = true)
        {
            _sessionId = sessionId;

            Title = $"AS400 Live Terminal Viewer - Session: {_sessionId}";
            Width = 900;
            Height = 620;
            MinWidth = 680;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BgBrush;
            Topmost = alwaysOnTop;

            BuildUi();
        }

        private void BuildUi()
        {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Screen
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status Footer

            // --- HEADER BAR ---
            var headerGrid = new Grid
            {
                Background = HeaderBgBrush,
                Height = 36
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = $"  AS400 / IBM 5250 Terminal Emulator  |  Session: {_sessionId}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(titleBlock, 0);
            headerGrid.Children.Add(titleBlock);

            _alwaysOnTopCheckBox = new CheckBox
            {
                Content = "Always on Top",
                IsChecked = Topmost,
                Foreground = TextMutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
                FontSize = 12
            };
            _alwaysOnTopCheckBox.Checked += (s, e) => Topmost = true;
            _alwaysOnTopCheckBox.Unchecked += (s, e) => Topmost = false;
            Grid.SetColumn(_alwaysOnTopCheckBox, 1);
            headerGrid.Children.Add(_alwaysOnTopCheckBox);

            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // --- 24x80 TERMINAL SCREEN ---
            var screenBorder = new Border
            {
                Background = TerminalBgBrush,
                BorderBrush = BorderSubtleBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(8),
                CornerRadius = new CornerRadius(3)
            };

            var viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(6)
            };

            _screenTextBlock = new TextBlock
            {
                FontFamily = new FontFamily("Consolas, Lucida Console, Courier New"),
                FontSize = 14,
                LineHeight = 18,
                FontWeight = FontWeights.Medium,
                Foreground = GreenTextBrush,
                TextWrapping = TextWrapping.NoWrap,
                Text = GetInitialBlankScreen()
            };

            viewbox.Child = _screenTextBlock;
            screenBorder.Child = viewbox;
            Grid.SetRow(screenBorder, 1);
            rootGrid.Children.Add(screenBorder);

            // --- FOOTER / STATUS STRIP ---
            var footerGrid = new Grid
            {
                Background = FooterBgBrush,
                Height = 30
            };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Session
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Conn Status
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Cursor
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Last Action

            _statusSessionTextBlock = new TextBlock
            {
                Text = $"Session: {_sessionId}",
                Foreground = TextMutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(10, 0, 15, 0)
            };
            Grid.SetColumn(_statusSessionTextBlock, 0);
            footerGrid.Children.Add(_statusSessionTextBlock);

            _statusConnTextBlock = new TextBlock
            {
                Text = "● Connected",
                Foreground = ConnOnlineBrush,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(_statusConnTextBlock, 1);
            footerGrid.Children.Add(_statusConnTextBlock);

            _statusCursorTextBlock = new TextBlock
            {
                Text = "Cursor: R 1, C 1",
                Foreground = TextMutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(_statusCursorTextBlock, 2);
            footerGrid.Children.Add(_statusCursorTextBlock);

            _statusActionTextBlock = new TextBlock
            {
                Text = "Status: Ready",
                Foreground = TextDimBrush,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(_statusActionTextBlock, 3);
            footerGrid.Children.Add(_statusActionTextBlock);

            Grid.SetRow(footerGrid, 2);
            rootGrid.Children.Add(footerGrid);

            Content = rootGrid;
        }

        public void UpdateScreen(string screenText, int cursorRow, int cursorCol)
        {
            if (Dispatcher.CheckAccess())
            {
                _screenTextBlock.Text = screenText ?? string.Empty;
                _statusCursorTextBlock.Text = $"Cursor: R {cursorRow}, C {cursorCol}";
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    _screenTextBlock.Text = screenText ?? string.Empty;
                    _statusCursorTextBlock.Text = $"Cursor: R {cursorRow}, C {cursorCol}";
                }));
            }
        }

        public void UpdateActionProgress(string progress)
        {
            if (string.IsNullOrWhiteSpace(progress)) return;
            string formatted = $"[{DateTime.Now:HH:mm:ss}] {progress}";

            if (Dispatcher.CheckAccess())
            {
                _statusActionTextBlock.Text = formatted;
                _statusActionTextBlock.Foreground = GreenTextBrush;
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    _statusActionTextBlock.Text = formatted;
                    _statusActionTextBlock.Foreground = GreenTextBrush;
                }));
            }
        }

        public void SetConnectionStatus(bool connected)
        {
            if (Dispatcher.CheckAccess())
            {
                _statusConnTextBlock.Text = connected ? "● Connected" : "○ Disconnected";
                _statusConnTextBlock.Foreground = connected ? ConnOnlineBrush : ConnOfflineBrush;
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    _statusConnTextBlock.Text = connected ? "● Connected" : "○ Disconnected";
                    _statusConnTextBlock.Foreground = connected ? ConnOnlineBrush : ConnOfflineBrush;
                }));
            }
        }

        private static string GetInitialBlankScreen()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 24; i++)
            {
                sb.AppendLine(new string(' ', 80));
            }
            return sb.ToString();
        }
    }
}
