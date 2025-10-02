using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Codeful.Models;
using Codeful.Services;

namespace Codeful
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly GroqService _groqService;
        private readonly ChatStorageService _chatStorage;
        private TextBlock? _currentThinkingText;
        private ChatData _currentChat;
        private List<ChatData> _allChats;

        public MainWindow()
        {
            InitializeComponent();
            _groqService = new GroqService();
            _chatStorage = new ChatStorageService();
            _currentChat = new ChatData();
            _allChats = new List<ChatData>();
            
            MessageInput.Focus();
            _ = LoadChatHistoryAsync();
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

        private async Task LoadChatHistoryAsync()
        {
            try
            {
                // Load chats on background thread
                var chats = await _chatStorage.LoadAllChatsAsync();
                
                // Update UI on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    _allChats = chats;
                    UpdateChatHistoryDisplay();
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Error loading chat history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void UpdateChatHistoryDisplay()
        {
            ChatHistoryPanel.Children.Clear();

            foreach (var chat in _allChats)
            {
                var chatCard = CreateChatCard(chat);
                ChatHistoryPanel.Children.Add(chatCard);
            }
        }

        private Border CreateChatCard(ChatData chat)
        {
            var border = new Border
            {
                Style = (Style)FindResource("ChatCardStyle"),
                Tag = chat
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var contentStack = new StackPanel();

            var titleText = new TextBlock
            {
                Text = chat.DisplayTitle,
                FontWeight = FontWeights.Medium,
                FontSize = 13,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var timestampText = new TextBlock
            {
                Text = chat.FormattedLastMessage,
                FontSize = 11,
                Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush")
            };

            contentStack.Children.Add(titleText);
            contentStack.Children.Add(timestampText);

            var deleteButton = new Button
            {
                Style = (Style)FindResource("ChatDeleteButtonStyle"),
                Tag = chat
            };

            var deletePath = new Path
            {
                Data = (Geometry)FindResource("CloseIcon"),
                Fill = new SolidColorBrush(Colors.Gray),
                Width = 10,
                Height = 10,
                Stretch = Stretch.Uniform
            };

            deleteButton.Content = deletePath;
            deleteButton.Click += DeleteChatButton_Click;

            grid.Children.Add(contentStack);
            Grid.SetColumn(contentStack, 0);
            grid.Children.Add(deleteButton);
            Grid.SetColumn(deleteButton, 1);

            border.Child = grid;
            border.MouseLeftButtonDown += ChatCard_Click;

            return border;
        }

        private void ChatCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is ChatData chat)
            {
                LoadChat(chat);
            }
        }

        private async void DeleteChatButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ChatData chat)
            {
                var result = MessageBox.Show($"Are you sure you want to delete this chat?\n\n\"{chat.DisplayTitle}\"", 
                    "Delete Chat", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _chatStorage.DeleteChatAsync(chat.Id);
                    
                    // If this was the current chat, start a new one
                    if (_currentChat.Id == chat.Id)
                    {
                        _currentChat = new ChatData();
                        ChatMessagesPanel.Children.Clear();
                    }

                    // Refresh chat history
                    await LoadChatHistoryAsync();
                }
            }
        }

        private void LoadChat(ChatData chat)
        {
            _currentChat = chat;
            ChatMessagesPanel.Children.Clear();

            foreach (var message in chat.Messages)
            {
                if (message.IsUser)
                {
                    AddUserMessage(message.Content);
                }
                else
                {
                    var aiResponse = new Services.AiResponse
                    {
                        ThinkingProcess = message.ThinkingProcess ?? string.Empty,
                        Conclusion = message.Content
                    };
                    AddAiResponseWithThinking(aiResponse);
                }
            }
        }

        private async Task SaveCurrentChat()
        {
            try
            {
                await _chatStorage.SaveChatAsync(_currentChat);
                
                // Refresh chat history if this is a new chat
                if (!_allChats.Any(c => c.Id == _currentChat.Id))
                {
                    await LoadChatHistoryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving chat: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            // Create new chat
            _currentChat = new ChatData();
            
            // Clear chat messages
            ChatMessagesPanel.Children.Clear();
            MessageInput.Focus();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsModal();
        }

        private void CloseModalButton_Click(object sender, RoutedEventArgs e)
        {
            HideSettingsModal();
        }

        private void ShowSettingsModal()
        {
            SettingsModalOverlay.Visibility = Visibility.Visible;
        }

        private void HideSettingsModal()
        {
            SettingsModalOverlay.Visibility = Visibility.Collapsed;
        }

        private async void DeleteAllDataButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete ALL chat data?\n\nThis action cannot be undone and will permanently remove all your saved conversations.",
                "Delete All Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var confirmResult = MessageBox.Show(
                    "This is your final warning.\n\nClick YES to permanently delete all your chat data.",
                    "Final Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Stop);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Delete all chats
                        await _chatStorage.DeleteAllChatsAsync();
                        
                        // Clear current chat
                        _currentChat = new ChatData();
                        ChatMessagesPanel.Children.Clear();
                        
                        // Refresh chat history (should be empty now)
                        await LoadChatHistoryAsync();
                        
                        // Hide settings modal
                        HideSettingsModal();
                        
                        MessageBox.Show("All chat data has been deleted.", "Data Deleted", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting data: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
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
                // Add user message to UI
                AddUserMessage(messageText);
                
                // Add user message to current chat
                var userMessage = new ChatMessage
                {
                    Content = messageText,
                    IsUser = true,
                    Timestamp = DateTime.Now
                };
                _currentChat.Messages.Add(userMessage);

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
                
                // Add AI message to current chat
                var aiMessage = new ChatMessage
                {
                    Content = response.Conclusion,
                    IsUser = false,
                    Timestamp = DateTime.Now,
                    ThinkingProcess = response.ThinkingProcess
                };
                _currentChat.Messages.Add(aiMessage);
                
                // Auto-save chat after AI response
                await SaveCurrentChat();

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
                await AnimateText(thinkingText, response.ThinkingProcess, 10);
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
            await AnimateRichText(conclusionRichText, response.Conclusion, 15);
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

            // Create the final formatted document once
            var finalDocument = CreateFormattedDocument(fullText);
            
            // Create a temporary document for animation
            richTextBox.Document.Blocks.Clear();
            
            // Simple character-by-character animation using plain text
            // to avoid recreating formatted documents repeatedly
            var tempParagraph = new Paragraph();
            richTextBox.Document.Blocks.Add(tempParagraph);
            
            for (int i = 0; i <= fullText.Length; i++)
            {
                await Task.Delay(delayMs);
                string currentText = fullText.Substring(0, i);
                
                // Update the paragraph with plain text during animation
                tempParagraph.Inlines.Clear();
                tempParagraph.Inlines.Add(new Run(currentText));
                
                // Scroll to bottom during animation
                ChatScrollViewer.ScrollToEnd();
            }
            
            // Replace with fully formatted document at the end
            richTextBox.Document = finalDocument;
            ChatScrollViewer.ScrollToEnd();
        }
        
        private FlowDocument CreateFormattedDocument(string text)
        {
            var document = new FlowDocument();
            
            var currentIndex = 0;
            var codeBlockRegex = new System.Text.RegularExpressions.Regex(@"```[\s\S]*?```", System.Text.RegularExpressions.RegexOptions.Multiline);
            var matches = codeBlockRegex.Matches(text);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Process text before this code block
                if (match.Index > currentIndex)
                {
                    var textBefore = text.Substring(currentIndex, match.Index - currentIndex);
                    if (!string.IsNullOrWhiteSpace(textBefore))
                    {
                        ProcessRegularText(textBefore, document);
                    }
                }
                
                // Process the code block
                var fullCodeBlock = match.Value;
                if (fullCodeBlock.Length > 6)
                {
                    // Extract code content (remove ``` from start and end)
                    var codeContent = fullCodeBlock.Substring(3, fullCodeBlock.Length - 6);
                    
                    // Remove language identifier if present (first line)
                    var lines = codeContent.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
                    if (lines.Length > 0 && lines[0].Trim().Length > 0 && !lines[0].Contains(' '))
                    {
                        // First line might be language identifier, remove it
                        codeContent = string.Join(Environment.NewLine, lines.Skip(1));
                    }
                    
                    codeContent = codeContent.Trim();
                    
                    if (!string.IsNullOrWhiteSpace(codeContent))
                    {
                        // Create code block UI
                        var codeContainer = new BlockUIContainer();
                        var codeBorder = new Border
                        {
                            Style = (Style)FindResource("CodeBlockStyle")
                        };
                        
                        var codeTextBlock = new TextBlock
                        {
                            Text = codeContent,
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            FontSize = 13,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                            TextWrapping = TextWrapping.Wrap
                        };
                        
                        codeBorder.Child = codeTextBlock;
                        codeContainer.Child = codeBorder;
                        document.Blocks.Add(codeContainer);
                    }
                }
                
                currentIndex = match.Index + match.Length;
            }
            
            // Process any remaining text after the last code block
            if (currentIndex < text.Length)
            {
                var remainingText = text.Substring(currentIndex);
                if (!string.IsNullOrWhiteSpace(remainingText))
                {
                    ProcessRegularText(remainingText, document);
                }
            }
            
            // If no code blocks found, process all text as regular text
            if (matches.Count == 0 && !string.IsNullOrWhiteSpace(text))
            {
                ProcessRegularText(text, document);
            }
            
            return document;
        }
        
        private void ProcessRegularText(string text, FlowDocument document)
        {
            // Split regular text into paragraphs
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var paragraphText in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraphText))
                    continue;
                    
                var paragraph = new Paragraph();
                ProcessInlineFormatting(paragraphText.Trim(), paragraph);
                
                // Only add paragraph if it has content
                if (paragraph.Inlines.Count > 0)
                {
                    document.Blocks.Add(paragraph);
                }
            }
        }
        
        private void ProcessInlineFormatting(string text, Paragraph paragraph)
        {
            // Single pass processing to avoid any duplication
            ProcessTextWithAllFormatting(text, paragraph);
        }
        
        private void ProcessTextWithAllFormatting(string text, Paragraph paragraph)
        {
            var currentIndex = 0;
            
            // Combined regex for all formatting: inline code, bold, italic
            var combinedRegex = new System.Text.RegularExpressions.Regex(
                @"(`[^`]+`)|" +                     // Inline code: `text`
                @"(\*\*\*(.+?)\*\*\*)|" +          // Bold + Italic: ***text***
                @"(___(.+?)___)|" +                 // Bold + Italic: ___text___
                @"(\*\*(.+?)\*\*)|" +               // Bold: **text**
                @"(__(.+?)__)|" +                   // Bold: __text__
                @"(\*(.+?)\*)|" +                   // Italic: *text*
                @"(_(.+?)_)"                        // Italic: _text_
            );
            
            var matches = combinedRegex.Matches(text);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Add plain text before this match
                if (match.Index > currentIndex)
                {
                    var plainText = text.Substring(currentIndex, match.Index - currentIndex);
                    if (!string.IsNullOrEmpty(plainText))
                    {
                        paragraph.Inlines.Add(new Run(plainText));
                    }
                }
                
                // Process the matched formatting
                if (match.Groups[1].Success) // Inline code: `text`
                {
                    var codeText = match.Groups[1].Value;
                    var actualCode = codeText.Substring(1, codeText.Length - 2); // Remove backticks
                    var codeRun = new Run(actualCode)
                    {
                        FontFamily = new FontFamily("Consolas, Courier New"),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                        FontSize = 13
                    };
                    paragraph.Inlines.Add(codeRun);
                }
                else if (match.Groups[2].Success) // ***text*** - Bold + Italic
                {
                    var run = new Run(match.Groups[3].Value)
                    {
                        FontWeight = FontWeights.Bold,
                        FontStyle = FontStyles.Italic
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (match.Groups[4].Success) // ___text___ - Bold + Italic
                {
                    var run = new Run(match.Groups[5].Value)
                    {
                        FontWeight = FontWeights.Bold,
                        FontStyle = FontStyles.Italic
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (match.Groups[6].Success) // **text** - Bold
                {
                    var run = new Run(match.Groups[7].Value)
                    {
                        FontWeight = FontWeights.Bold
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (match.Groups[8].Success) // __text__ - Bold
                {
                    var run = new Run(match.Groups[9].Value)
                    {
                        FontWeight = FontWeights.Bold
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (match.Groups[10].Success) // *text* - Italic
                {
                    var run = new Run(match.Groups[11].Value)
                    {
                        FontStyle = FontStyles.Italic
                    };
                    paragraph.Inlines.Add(run);
                }
                else if (match.Groups[12].Success) // _text_ - Italic
                {
                    var run = new Run(match.Groups[13].Value)
                    {
                        FontStyle = FontStyles.Italic
                    };
                    paragraph.Inlines.Add(run);
                }
                
                currentIndex = match.Index + match.Length;
            }
            
            // Add any remaining plain text after the last match
            if (currentIndex < text.Length)
            {
                var remainingText = text.Substring(currentIndex);
                if (!string.IsNullOrEmpty(remainingText))
                {
                    paragraph.Inlines.Add(new Run(remainingText));
                }
            }
            
            // If no matches at all, add the entire text as plain text
            if (matches.Count == 0 && !string.IsNullOrEmpty(text))
            {
                paragraph.Inlines.Add(new Run(text));
            }
        }
    }
}