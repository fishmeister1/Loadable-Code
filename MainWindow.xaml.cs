using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Codeful.Services;

namespace Codeful
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly GroqService _groqService;
        private TextBlock? _currentThinkingText;

        public MainWindow()
        {
            InitializeComponent();
            _groqService = new GroqService();
            MessageInput.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            var maximizeIcon = MaximizeButton.Content as Path;
            if (maximizeIcon == null)
            {
                // If content is not directly a Path, find it in the button's content
                maximizeIcon = FindVisualChild<Path>(MaximizeButton);
            }

            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                // Update icon to show maximize icon
                if (maximizeIcon != null)
                    maximizeIcon.Data = (Geometry)FindResource("MaximizeIcon");
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                // Update icon to show restore icon
                if (maximizeIcon != null)
                    maximizeIcon.Data = (Geometry)FindResource("RestoreIcon");
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear chat messages
            ChatMessagesPanel.Children.Clear();
            MessageInput.Focus();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for settings functionality
            MessageBox.Show("Settings functionality would go here.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable send button only if there's text
            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(MessageInput.Text);
            
            // Auto-expand the input field
            AdjustInputHeight();
        }

        private void AdjustInputHeight()
        {
            var textBox = MessageInput;
            
            // Find the containing border (now the textbox is inside a Grid which is inside the Border)
            var grid = textBox.Parent as Grid;
            var border = grid?.Parent as Border;
            
            if (border == null) return;
            
            // Measure the text size
            var formattedText = new FormattedText(
                textBox.Text + "W", // Add extra character for padding
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                textBox.FontSize,
                textBox.Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerInchX / 96.0);

            // Calculate required height
            var availableWidth = textBox.ActualWidth - textBox.Padding.Left - textBox.Padding.Right;
            if (availableWidth > 0)
            {
                formattedText.MaxTextWidth = availableWidth;
                var requiredHeight = formattedText.Height + textBox.Padding.Top + textBox.Padding.Bottom;
                
                // Set minimum height (single line) and maximum height
                var minHeight = 44.0;
                var maxHeight = 120.0;
                
                var newHeight = Math.Max(minHeight, Math.Min(maxHeight, requiredHeight + 8));
                
                if (Math.Abs(textBox.Height - newHeight) > 1)
                {
                    textBox.Height = newHeight;
                    // Don't set border height since it contains other elements now
                }
            }
        }

        private async void SendMessage()
        {
            var messageText = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(messageText))
                return;
                
            // Capitalize the first letter
            if (!string.IsNullOrEmpty(messageText))
            {
                messageText = char.ToUpper(messageText[0]) + messageText.Substring(1);
            }

            // Disable input while processing
            MessageInput.IsEnabled = false;
            SendButton.IsEnabled = false;

            try
            {
                // Add user message
                AddUserMessage(messageText);

                // Clear input
                MessageInput.Text = string.Empty;

                // Add divider
                AddDivider();

                // Add thinking container
                var thinkingContainer = CreateThinkingContainer();
                ChatMessagesPanel.Children.Add(thinkingContainer);

                // Scroll to bottom
                ChatScrollViewer.ScrollToEnd();

                // Get AI response with thinking process
                var response = await _groqService.SendMessageAsync(messageText, OnThinkingUpdate);

                // Remove the thinking container since we'll display it differently
                if (ChatMessagesPanel.Children.Count > 0 && 
                    ChatMessagesPanel.Children[ChatMessagesPanel.Children.Count - 1] is StackPanel)
                {
                    ChatMessagesPanel.Children.RemoveAt(ChatMessagesPanel.Children.Count - 1);
                }

                // Add final AI response with thinking and conclusion
                AddAiResponseWithThinking(response);

                // Scroll to bottom
                ChatScrollViewer.ScrollToEnd();
            }
            catch (Exception ex)
            {
                AddAiResponse($"Error: {ex.Message}");
            }
            finally
            {
                // Re-enable input
                MessageInput.IsEnabled = true;
                SendButton.IsEnabled = true;
                MessageInput.Focus();
            }
        }

        private void AddUserMessage(string text)
        {
            var messageContainer = new Border
            {
                Style = (Style)FindResource("MessageContainerStyle")
            };

            var messageText = new TextBlock
            {
                Text = text,
                Style = (Style)FindResource("UserMessageTextStyle")
            };

            messageContainer.Child = messageText;
            ChatMessagesPanel.Children.Add(messageContainer);
        }

        private void AddDivider()
        {
            var divider = new Border
            {
                Style = (Style)FindResource("MessageDividerStyle")
            };
            ChatMessagesPanel.Children.Add(divider);
        }

        private StackPanel CreateThinkingContainer()
        {
            var container = new StackPanel
            {
                Margin = new Thickness(16, 0, 16, 0)
            };

            _currentThinkingText = new TextBlock
            {
                Style = (Style)FindResource("ThinkingTextStyle"),
                Text = "Initializing..."
            };

            container.Children.Add(_currentThinkingText);
            return container;
        }

        private void OnThinkingUpdate(string thinkingText)
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentThinkingText != null)
                {
                    _currentThinkingText.Text = thinkingText;
                }
            });
        }

        private async void AddAiResponseWithThinking(Services.AiResponse response)
        {
            var container = new Border
            {
                Style = (Style)FindResource("MessageContainerStyle")
            };

            var stackPanel = new StackPanel();

            // Add thinking process if it exists
            if (!string.IsNullOrEmpty(response.ThinkingProcess))
            {
                // Add bold "Thought Process" label
                var thinkingLabel = new TextBlock
                {
                    Text = "Thought Process",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                    Margin = new Thickness(0, 0, 0, 4),
                    FontFamily = new FontFamily("pack://application:,,,/Resources/#Epilogue")
                };

                stackPanel.Children.Add(thinkingLabel);

                // Add thinking text with typing animation
                var thinkingText = new TextBlock
                {
                    Text = "",
                    Style = (Style)FindResource("ThinkingTextStyle"),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                stackPanel.Children.Add(thinkingText);
                
                container.Child = stackPanel;
                ChatMessagesPanel.Children.Add(container);
                ChatScrollViewer.ScrollToEnd();
                
                // Animate thinking text
                await AnimateText(thinkingText, response.ThinkingProcess, 12);
            }

            // Add conclusion label
            var conclusionLabel = new TextBlock
            {
                Text = "Conclusion",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                Margin = new Thickness(0, 8, 0, 4),
                FontFamily = new FontFamily("pack://application:,,,/Resources/#Epilogue")
            };

            stackPanel.Children.Add(conclusionLabel);

            // Add conclusion text with rich formatting and typing animation
            var conclusionRichText = new RichTextBox
            {
                Style = (Style)FindResource("AiRichTextStyle")
            };

            stackPanel.Children.Add(conclusionRichText);
            
            if (string.IsNullOrEmpty(response.ThinkingProcess))
            {
                container.Child = stackPanel;
                ChatMessagesPanel.Children.Add(container);
            }
            
            ChatScrollViewer.ScrollToEnd();
            
            // Animate conclusion text with rich formatting
            await AnimateRichText(conclusionRichText, response.Conclusion, 8);
        }

        private void AddAiResponse(string text)
        {
            // Fallback method for simple text responses
            var response = new Services.AiResponse { Conclusion = text };
            AddAiResponseWithThinking(response);
        }

        private async Task AnimateText(TextBlock textBlock, string fullText, int delayMs)
        {
            if (string.IsNullOrEmpty(fullText))
            {
                textBlock.Text = "";
                return;
            }

            textBlock.Text = "";
            
            for (int i = 0; i <= fullText.Length; i++)
            {
                await Task.Delay(delayMs);
                textBlock.Text = fullText.Substring(0, i);
                
                // Scroll to bottom during animation
                ChatScrollViewer.ScrollToEnd();
            }
        }
        
        private async Task AnimateRichText(RichTextBox richTextBox, string fullText, int delayMs)
        {
            if (string.IsNullOrEmpty(fullText))
            {
                richTextBox.Document.Blocks.Clear();
                return;
            }

            richTextBox.Document.Blocks.Clear();
            
            for (int i = 0; i <= fullText.Length; i++)
            {
                await Task.Delay(delayMs);
                string currentText = fullText.Substring(0, i);
                
                // Create formatted document
                richTextBox.Document = CreateFormattedDocument(currentText);
                
                // Scroll to bottom during animation
                ChatScrollViewer.ScrollToEnd();
            }
        }
        
        private FlowDocument CreateFormattedDocument(string text)
        {
            var document = new FlowDocument();
            var paragraph = new Paragraph();
            
            // Parse text for code blocks (```...```)
            var parts = System.Text.RegularExpressions.Regex.Split(text, @"(```[\s\S]*?```)");
            
            foreach (var part in parts)
            {
                if (part.StartsWith("```") && part.EndsWith("```") && part.Length > 6)
                {
                    // This is a code block
                    var codeText = part.Substring(3, part.Length - 6).Trim();
                    
                    // Create code block container
                    var codeContainer = new BlockUIContainer();
                    var codeBorder = new Border
                    {
                        Style = (Style)FindResource("CodeBlockStyle")
                    };
                    
                    var codeTextBlock = new TextBlock
                    {
                        Text = codeText,
                        FontFamily = new FontFamily("Consolas, Courier New"),
                        FontSize = 13,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                        TextWrapping = TextWrapping.Wrap
                    };
                    
                    codeBorder.Child = codeTextBlock;
                    codeContainer.Child = codeBorder;
                    document.Blocks.Add(codeContainer);
                    
                    // Start new paragraph after code block
                    paragraph = new Paragraph();
                }
                else if (!string.IsNullOrEmpty(part))
                {
                    // Regular text
                    paragraph.Inlines.Add(new Run(part));
                }
            }
            
            // Add the last paragraph if it has content
            if (paragraph.Inlines.Count > 0)
            {
                document.Blocks.Add(paragraph);
            }
            
            return document;
        }
    }
}