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
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Media;
using System.Runtime.InteropServices;
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
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationService;
        private TextBlock? _currentThinkingText;
        private Border? _loadingIcon;
        private Border? _delayedMessage;
        private System.Windows.Threading.DispatcherTimer? _delayedMessageTimer;
        private ChatData _currentChat;
        private List<ChatData> _allChats;
        private UserSettings _userSettings;
        private bool _isLoadingPastChat = false;

        public MainWindow()
        {
            InitializeComponent();
            _groqService = new GroqService();
            _chatStorage = new ChatStorageService();
            _settingsService = new SettingsService();
            _notificationService = new NotificationService();
            _currentChat = new ChatData();
            _allChats = new List<ChatData>();
            _userSettings = new UserSettings();
            
            MessageInput.Focus();
            _ = LoadChatHistoryAsync();
            _ = LoadUserSettingsAsync();
            
            // Show initial welcome message after settings are loaded
            _ = Task.Delay(100).ContinueWith(_ => Dispatcher.InvokeAsync(ShowWelcomeMessage));
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

        private async Task LoadUserSettingsAsync()
        {
            try
            {
                _userSettings = await _settingsService.LoadSettingsAsync();
                
                // Update UI with loaded settings
                await Dispatcher.InvokeAsync(() =>
                {
                    NameSettingTextBox.Text = _userSettings.Name;
                    NotificationsToggle.IsChecked = _userSettings.Notifications;
                    
                    // Refresh welcome message if current chat is empty
                    if (_currentChat.Messages.Count == 0)
                    {
                        ShowWelcomeMessage();
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Error loading user settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async void NameSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && _userSettings != null)
            {
                _userSettings.Name = textBox.Text;
                try
                {
                    await _settingsService.SaveSettingsAsync(_userSettings);
                    
                    // Refresh welcome message if current chat is empty
                    if (_currentChat.Messages.Count == 0)
                    {
                        ShowWelcomeMessage();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving user settings: {ex.Message}");
                }
            }
        }

        private async void NotificationsToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && _userSettings != null)
            {
                _userSettings.Notifications = checkBox.IsChecked ?? false;
                try
                {
                    await _settingsService.SaveSettingsAsync(_userSettings);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving user settings: {ex.Message}");
                }
            }
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
                var result = CustomConfirmationDialog.ShowDialog(
                    this, 
                    "Delete Chat", 
                    "Are you sure you want to delete this chat?", 
                    $"\"{chat.DisplayTitle}\"");

                if (result)
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
            // Set flag to disable ALL animations during past chat loading
            _isLoadingPastChat = true;
            
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
            
            // Re-enable animations for future new messages
            _isLoadingPastChat = false;
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

        private string GetTimeBasedGreeting()
        {
            var hour = DateTime.Now.Hour;
            if (hour >= 5 && hour < 12)
                return "Good morning";
            else if (hour >= 12 && hour < 17)
                return "Good afternoon";
            else
                return "Good evening";
        }

        private void ShowWelcomeMessage()
        {
            // Clear any existing messages
            ChatMessagesPanel.Children.Clear();

            // Create welcome container
            var welcomeContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(40)
            };

            var welcomeStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Create greeting text
            var greeting = GetTimeBasedGreeting();
            var userName = !string.IsNullOrWhiteSpace(_userSettings?.Name) ? $" {_userSettings.Name}" : "";
            var greetingText = new TextBlock
            {
                Text = $"{greeting}{userName},",
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                FontFamily = new FontFamily("pack://application:,,,/Resources/#Epilogue")
            };

            // Create subtitle text
            var subtitleText = new TextBlock
            {
                Text = "What would you like to code today?",
                FontSize = 14,
                Foreground = (SolidColorBrush)FindResource("SecondaryTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("pack://application:,,,/Resources/#Epilogue")
            };

            welcomeStack.Children.Add(greetingText);
            welcomeStack.Children.Add(subtitleText);
            welcomeContainer.Children.Add(welcomeStack);

            // Add to chat area
            ChatMessagesPanel.Children.Add(welcomeContainer);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            // Create new chat
            _currentChat = new ChatData();
            
            // Show welcome message
            ShowWelcomeMessage();
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
                        
                        // Delete user settings
                        await _settingsService.DeleteSettingsAsync();
                        
                        // Reset current chat and settings
                        _currentChat = new ChatData();
                        _userSettings = new UserSettings();
                        ChatMessagesPanel.Children.Clear();
                        NameSettingTextBox.Text = string.Empty;
                        NotificationsToggle.IsChecked = true;
                        
                        // Refresh chat history (should be empty now)
                        await LoadChatHistoryAsync();
                        
                        // Hide settings modal
                        HideSettingsModal();
                        
                        MessageBox.Show("All chat data and settings have been deleted.", "Data Deleted", 
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
            if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    // Shift+Enter: Insert new line manually since AcceptsReturn is false
                    e.Handled = true;
                    int caretIndex = MessageInput.CaretIndex;
                    MessageInput.Text = MessageInput.Text.Insert(caretIndex, Environment.NewLine);
                    MessageInput.CaretIndex = caretIndex + Environment.NewLine.Length;
                }
                else
                {
                    // Enter alone: Send message
                    e.Handled = true;
                    SendMessage();
                }
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

                // Show loading icon (temporarily disabled for testing)
                // ShowLoadingIcon();

                // Get AI response (thought process will be removed in parsing)
                var response = await _groqService.SendMessageAsync(messageText, null);

                // Ensure we're on the UI thread for the next operations
                await Dispatcher.InvokeAsync(async () =>
                {
                    // Hide loading icon first (temporarily disabled for testing)
                    // HideLoadingIcon();
                    
                    // Small delay to prevent race conditions
                    await Task.Delay(10);
                    
                    // Add final AI response with proper formatting
                    AddAiResponseWithThinking(response, false);
                });
                
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
                // Hide loading icon in case of error (temporarily disabled for testing)
                // HideLoadingIcon();
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

        private void AddAiResponseWithThinking(Services.AiResponse response, bool animate = true)
        {
            // STEP 2: Show the information (thought process already removed in parsing)
            var displayText = !string.IsNullOrEmpty(response.Conclusion) ? response.Conclusion : "No response received";
            
            // STEP 3: Format the text correctly (only once, after cleanup)
            var container = new Border
            {
                Style = (Style)FindResource("MessageContainerStyle")
            };

            var stackPanel = new StackPanel();

            // Create RichTextBox for formatted display
            var conclusionRichText = new RichTextBox
            {
                Style = (Style)FindResource("AiRichTextStyle"),
                Focusable = false
            };

            // Apply formatting to the clean text
            var formattedDocument = CreateFormattedDocument(displayText);
            conclusionRichText.Document = formattedDocument;

            stackPanel.Children.Add(conclusionRichText);
            container.Child = stackPanel;
            
            // Add to UI in a single operation to prevent race conditions
            Dispatcher.Invoke(() =>
            {
                ChatMessagesPanel.Children.Add(container);
                ChatScrollViewer.ScrollToEnd();
            });
        }


        private void AddAiResponse(string text)
        {
            // Fallback method for simple text responses
            var response = new Services.AiResponse { Conclusion = text };
            AddAiResponseWithThinking(response, false);
        }

        private FlowDocument CreateFormattedDocument(string text)
        {
            var document = new FlowDocument();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                // Add empty paragraph if no text
                document.Blocks.Add(new Paragraph(new Run("No content")));
                return document;
            }

            // STEP 3: Format the text correctly (single pass only)
            try
            {
                ProcessTextContent(text, document);
            }
            catch (Exception ex)
            {
                // Fallback for formatting errors
                document.Blocks.Clear();
                document.Blocks.Add(new Paragraph(new Run(text)));
                System.Diagnostics.Debug.WriteLine($"Formatting error: {ex.Message}");
            }
            
            return document;
        }

        private void ProcessTextContent(string text, FlowDocument document)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var currentParagraph = new Paragraph();
            var inCodeBlock = false;
            var codeBlockLines = new List<string>();
            var codeBlockLanguage = "";

            foreach (var line in lines)
            {
                // Check for code block start/end
                if (line.Trim().StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        // Starting code block
                        inCodeBlock = true;
                        codeBlockLanguage = line.Trim().Substring(3).Trim();
                        codeBlockLines.Clear();
                        
                        // Add current paragraph if it has content
                        if (currentParagraph.Inlines.Count > 0)
                        {
                            document.Blocks.Add(currentParagraph);
                            currentParagraph = new Paragraph();
                        }
                    }
                    else
                    {
                        // Ending code block
                        inCodeBlock = false;
                        AddCodeBlock(document, codeBlockLines, codeBlockLanguage);
                        codeBlockLines.Clear();
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockLines.Add(line);
                }
                else
                {
                    // Regular line - no special formatting, just plain text
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        // Empty line - start new paragraph
                        if (currentParagraph.Inlines.Count > 0)
                        {
                            document.Blocks.Add(currentParagraph);
                            currentParagraph = new Paragraph();
                        }
                    }
                    else
                    {
                        // Add plain text line
                        currentParagraph.Inlines.Add(new Run(line));
                        currentParagraph.Inlines.Add(new LineBreak());
                    }
                }
            }

            // Add final paragraph if it has content
            if (currentParagraph.Inlines.Count > 0)
            {
                // Remove trailing line break if present
                if (currentParagraph.Inlines.LastInline is LineBreak)
                {
                    currentParagraph.Inlines.Remove(currentParagraph.Inlines.LastInline);
                }
                document.Blocks.Add(currentParagraph);
            }

            // Handle unclosed code block
            if (inCodeBlock && codeBlockLines.Count > 0)
            {
                AddCodeBlock(document, codeBlockLines, codeBlockLanguage);
            }
        }



        private void AddCodeBlock(FlowDocument document, List<string> codeLines, string language)
        {
            if (codeLines.Count == 0)
                return;

            var codeContent = string.Join(Environment.NewLine, codeLines);
            
            var codeContainer = new BlockUIContainer();
            
            // Create main container for code block with copy button
            var mainContainer = new Grid();
            mainContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            // Create header with copy button
            var headerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F0FE")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D9E0")),
                BorderThickness = new Thickness(1, 1, 1, 0),
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Padding = new Thickness(8, 4, 8, 4)
            };
            
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            // Language label
            var languageLabel = new TextBlock
            {
                Text = string.IsNullOrEmpty(language) ? "code" : language.ToUpper(),
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#586069")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(languageLabel, 0);
            
            // Copy button
            var copyButton = new Button
            {
                Content = "Copy",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6F8FA")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#24292E")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DA")),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = codeContent // Store the code content in the tag for copying
            };
            
            copyButton.Click += CopyButton_Click;
            
            // Add hover effects
            copyButton.MouseEnter += (s, e) =>
            {
                copyButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E4E8"));
            };
            
            copyButton.MouseLeave += (s, e) =>
            {
                copyButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6F8FA"));
            };
            
            Grid.SetColumn(copyButton, 1);
            
            headerGrid.Children.Add(languageLabel);
            headerGrid.Children.Add(copyButton);
            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            
            // Create code content border
            var codeBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6F8FA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D9E0")),
                BorderThickness = new Thickness(1, 0, 1, 1),
                CornerRadius = new CornerRadius(0, 0, 6, 6),
                Padding = new Thickness(12)
            };
            
            // Create a RichTextBox for syntax highlighting
            var codeRichTextBox = new RichTextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 13,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                IsTabStop = false,
                Focusable = false
            };
            
            ScrollViewer.SetVerticalScrollBarVisibility(codeRichTextBox, ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollBarVisibility(codeRichTextBox, ScrollBarVisibility.Disabled);
            
            // Apply syntax highlighting
            var highlightedDocument = ApplySyntaxHighlighting(codeContent, language.ToLower());
            codeRichTextBox.Document = highlightedDocument;
            
            codeBorder.Child = codeRichTextBox;
            Grid.SetRow(codeBorder, 1);
            
            mainContainer.Children.Add(headerBorder);
            mainContainer.Children.Add(codeBorder);
            
            codeContainer.Child = mainContainer;
            document.Blocks.Add(codeContainer);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string codeContent)
            {
                try
                {
                    Clipboard.SetText(codeContent);
                    
                    // Provide visual feedback
                    var originalContent = button.Content;
                    button.Content = "Copied!";
                    button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                    
                    // Reset after 2 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    
                    timer.Tick += (s, args) =>
                    {
                        button.Content = originalContent;
                        button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#24292E"));
                        timer.Stop();
                    };
                    
                    timer.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
                    
                    // Show error feedback
                    var originalContent = button.Content;
                    button.Content = "Error";
                    button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D73A49"));
                    
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    
                    timer.Tick += (s, args) =>
                    {
                        button.Content = originalContent;
                        button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#24292E"));
                        timer.Stop();
                    };
                    
                    timer.Start();
                }
            }
        }

        private FlowDocument ApplySyntaxHighlighting(string code, string language)
        {
            var document = new FlowDocument();
            var paragraph = new Paragraph();
            
            if (string.IsNullOrEmpty(code))
            {
                document.Blocks.Add(paragraph);
                return document;
            }

            switch (language.ToLower())
            {
                case "csharp":
                case "c#":
                case "cs":
                    HighlightCSharp(code, paragraph);
                    break;
                case "javascript":
                case "js":
                    HighlightJavaScript(code, paragraph);
                    break;
                case "python":
                case "py":
                    HighlightPython(code, paragraph);
                    break;
                case "html":
                    HighlightHtml(code, paragraph);
                    break;
                case "css":
                    HighlightCss(code, paragraph);
                    break;
                case "json":
                    HighlightJson(code, paragraph);
                    break;
                case "xml":
                    HighlightXml(code, paragraph);
                    break;
                case "sql":
                    HighlightSql(code, paragraph);
                    break;
                default:
                    // Default highlighting for unknown languages
                    HighlightGeneric(code, paragraph);
                    break;
            }
            
            document.Blocks.Add(paragraph);
            return document;
        }

        private void HighlightCSharp(string code, Paragraph paragraph)
        {
            var keywords = new[] { "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", 
                "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", 
                "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", 
                "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", 
                "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", 
                "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", 
                "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", 
                "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", 
                "void", "volatile", "while" };
            
            HighlightWithKeywords(code, paragraph, keywords, "#0000FF", "#008000", "#FF0000");
        }

        private void HighlightJavaScript(string code, Paragraph paragraph)
        {
            var keywords = new[] { "abstract", "await", "boolean", "break", "byte", "case", "catch", "char", 
                "class", "const", "continue", "debugger", "default", "delete", "do", "double", "else", 
                "enum", "export", "extends", "false", "final", "finally", "float", "for", "function", 
                "goto", "if", "implements", "import", "in", "instanceof", "int", "interface", "let", 
                "long", "native", "new", "null", "package", "private", "protected", "public", "return", 
                "short", "static", "super", "switch", "synchronized", "this", "throw", "throws", "transient", 
                "true", "try", "typeof", "var", "void", "volatile", "while", "with", "yield" };
            
            HighlightWithKeywords(code, paragraph, keywords, "#0000FF", "#008000", "#FF0000");
        }

        private void HighlightPython(string code, Paragraph paragraph)
        {
            var keywords = new[] { "and", "as", "assert", "break", "class", "continue", "def", "del", "elif", 
                "else", "except", "exec", "finally", "for", "from", "global", "if", "import", "in", "is", 
                "lambda", "not", "or", "pass", "print", "raise", "return", "try", "while", "with", "yield", 
                "False", "None", "True" };
            
            HighlightWithKeywords(code, paragraph, keywords, "#0000FF", "#008000", "#FF0000");
        }

        private void HighlightHtml(string code, Paragraph paragraph)
        {
            var currentIndex = 0;
            var tagRegex = new System.Text.RegularExpressions.Regex(@"<[^>]+>");
            var matches = tagRegex.Matches(code);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Add text before tag
                if (match.Index > currentIndex)
                {
                    var beforeText = code.Substring(currentIndex, match.Index - currentIndex);
                    paragraph.Inlines.Add(new Run(beforeText) { Foreground = new SolidColorBrush(Colors.Black) });
                }

                // Add highlighted tag
                paragraph.Inlines.Add(new Run(match.Value) { Foreground = new SolidColorBrush(Colors.Blue) });
                currentIndex = match.Index + match.Length;
            }

            // Add remaining text
            if (currentIndex < code.Length)
            {
                paragraph.Inlines.Add(new Run(code.Substring(currentIndex)) { Foreground = new SolidColorBrush(Colors.Black) });
            }
        }

        private void HighlightCss(string code, Paragraph paragraph)
        {
            var properties = new[] { "color", "background", "font-size", "margin", "padding", "border", 
                "width", "height", "display", "position", "top", "left", "right", "bottom", "float", 
                "clear", "overflow", "text-align", "font-family", "font-weight" };
            
            HighlightWithKeywords(code, paragraph, properties, "#800080", "#008000", "#FF0000");
        }

        private void HighlightJson(string code, Paragraph paragraph)
        {
            var currentIndex = 0;
            var stringRegex = new System.Text.RegularExpressions.Regex(@"""[^""\\]*(?:\\.[^""\\]*)*""");
            var numberRegex = new System.Text.RegularExpressions.Regex(@"\b\d+(?:\.\d+)?\b");
            var booleanRegex = new System.Text.RegularExpressions.Regex(@"\b(true|false|null)\b");

            var allMatches = new List<(int Index, int Length, string Type, string Value)>();

            foreach (System.Text.RegularExpressions.Match match in stringRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "string", match.Value));
            
            foreach (System.Text.RegularExpressions.Match match in numberRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "number", match.Value));
            
            foreach (System.Text.RegularExpressions.Match match in booleanRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "boolean", match.Value));

            allMatches.Sort((a, b) => a.Index.CompareTo(b.Index));

            foreach (var match in allMatches)
            {
                if (match.Index > currentIndex)
                {
                    var beforeText = code.Substring(currentIndex, match.Index - currentIndex);
                    paragraph.Inlines.Add(new Run(beforeText) { Foreground = new SolidColorBrush(Colors.Black) });
                }

                Color color = match.Type switch
                {
                    "string" => Colors.Red,
                    "number" => Colors.Blue,
                    "boolean" => Colors.Purple,
                    _ => Colors.Black
                };

                paragraph.Inlines.Add(new Run(match.Value) { Foreground = new SolidColorBrush(color) });
                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < code.Length)
            {
                paragraph.Inlines.Add(new Run(code.Substring(currentIndex)) { Foreground = new SolidColorBrush(Colors.Black) });
            }
        }

        private void HighlightXml(string code, Paragraph paragraph)
        {
            HighlightHtml(code, paragraph); // XML uses similar highlighting to HTML
        }

        private void HighlightSql(string code, Paragraph paragraph)
        {
            var keywords = new[] { "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", 
                "DROP", "TABLE", "INDEX", "VIEW", "DATABASE", "SCHEMA", "PROCEDURE", "FUNCTION", "TRIGGER", 
                "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "ON", "AS", "AND", "OR", "NOT", "NULL", 
                "IS", "IN", "BETWEEN", "LIKE", "ORDER", "BY", "GROUP", "HAVING", "DISTINCT", "TOP", "LIMIT" };
            
            HighlightWithKeywords(code, paragraph, keywords, "#0000FF", "#008000", "#FF0000");
        }

        private void HighlightGeneric(string code, Paragraph paragraph)
        {
            // Basic highlighting for unknown languages
            var stringRegex = new System.Text.RegularExpressions.Regex(@"""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*'");
            var commentRegex = new System.Text.RegularExpressions.Regex(@"//.*$|/\*[\s\S]*?\*/|#.*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            var allMatches = new List<(int Index, int Length, string Type)>();
            
            foreach (System.Text.RegularExpressions.Match match in stringRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "string"));
            
            foreach (System.Text.RegularExpressions.Match match in commentRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "comment"));

            allMatches.Sort((a, b) => a.Index.CompareTo(b.Index));

            var currentIndex = 0;
            foreach (var match in allMatches)
            {
                if (match.Index > currentIndex)
                {
                    var beforeText = code.Substring(currentIndex, match.Index - currentIndex);
                    paragraph.Inlines.Add(new Run(beforeText) { Foreground = new SolidColorBrush(Colors.Black) });
                }

                Color color = match.Type == "string" ? Colors.Red : Colors.Green;
                var matchText = code.Substring(match.Index, match.Length);
                paragraph.Inlines.Add(new Run(matchText) { Foreground = new SolidColorBrush(color) });
                
                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < code.Length)
            {
                paragraph.Inlines.Add(new Run(code.Substring(currentIndex)) { Foreground = new SolidColorBrush(Colors.Black) });
            }
        }

        private void HighlightWithKeywords(string code, Paragraph paragraph, string[] keywords, string keywordColor, string commentColor, string stringColor)
        {
            var keywordRegex = new System.Text.RegularExpressions.Regex(@"\b(" + string.Join("|", keywords) + @")\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var stringRegex = new System.Text.RegularExpressions.Regex(@"""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*'");
            var commentRegex = new System.Text.RegularExpressions.Regex(@"//.*$|/\*[\s\S]*?\*/", System.Text.RegularExpressions.RegexOptions.Multiline);

            var allMatches = new List<(int Index, int Length, string Type)>();

            foreach (System.Text.RegularExpressions.Match match in keywordRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "keyword"));
            
            foreach (System.Text.RegularExpressions.Match match in stringRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "string"));
            
            foreach (System.Text.RegularExpressions.Match match in commentRegex.Matches(code))
                allMatches.Add((match.Index, match.Length, "comment"));

            // Sort by position and remove overlaps
            allMatches.Sort((a, b) => a.Index.CompareTo(b.Index));
            var nonOverlapping = new List<(int Index, int Length, string Type)>();
            var lastEnd = 0;

            foreach (var match in allMatches)
            {
                if (match.Index >= lastEnd)
                {
                    nonOverlapping.Add(match);
                    lastEnd = match.Index + match.Length;
                }
            }

            var currentIndex = 0;
            foreach (var match in nonOverlapping)
            {
                if (match.Index > currentIndex)
                {
                    var beforeText = code.Substring(currentIndex, match.Index - currentIndex);
                    paragraph.Inlines.Add(new Run(beforeText) { Foreground = new SolidColorBrush(Colors.Black) });
                }

                Color color = match.Type switch
                {
                    "keyword" => (Color)ColorConverter.ConvertFromString(keywordColor),
                    "string" => (Color)ColorConverter.ConvertFromString(stringColor),
                    "comment" => (Color)ColorConverter.ConvertFromString(commentColor),
                    _ => Colors.Black
                };

                var matchText = code.Substring(match.Index, match.Length);
                paragraph.Inlines.Add(new Run(matchText) { Foreground = new SolidColorBrush(color) });
                
                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < code.Length)
            {
                paragraph.Inlines.Add(new Run(code.Substring(currentIndex)) { Foreground = new SolidColorBrush(Colors.Black) });
            }
        }



        private void ShowLoadingIcon()
        {
            // Create loading icon
            var loadingBorder = new Border
            {
                Style = (Style)FindResource("LoadingIconStyle")
            };

            // Create container for the loading icon (similar to AI response container)
            var container = new Border
            {
                Style = (Style)FindResource("LoadingContainerStyle")
            };

            container.Child = loadingBorder;
            
            // Store reference to remove later
            _loadingIcon = container;
            
            // Add to chat panel
            ChatMessagesPanel.Children.Add(container);
            ChatScrollViewer.ScrollToEnd();
            
            // Start timer for delayed message
            StartDelayedMessageTimer();
        }

        private void HideLoadingIcon()
        {
            Dispatcher.Invoke(() =>
            {
                // Stop and cleanup timer
                StopDelayedMessageTimer();
                
                // Remove delayed message if it exists
                if (_delayedMessage != null)
                {
                    if (ChatMessagesPanel.Children.Contains(_delayedMessage))
                    {
                        ChatMessagesPanel.Children.Remove(_delayedMessage);
                    }
                    _delayedMessage = null;
                }
                
                // Remove loading icon
                if (_loadingIcon != null)
                {
                    if (ChatMessagesPanel.Children.Contains(_loadingIcon))
                    {
                        ChatMessagesPanel.Children.Remove(_loadingIcon);
                    }
                    _loadingIcon = null;
                }
                
                // Force layout update
                ChatMessagesPanel.UpdateLayout();
            });
        }

        private void StartDelayedMessageTimer()
        {
            _delayedMessageTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(2000)
            };
            
            _delayedMessageTimer.Tick += (s, e) =>
            {
                ShowDelayedMessage();
                _delayedMessageTimer?.Stop();
            };
            
            _delayedMessageTimer.Start();
        }

        private void StopDelayedMessageTimer()
        {
            if (_delayedMessageTimer != null)
            {
                _delayedMessageTimer.Stop();
                _delayedMessageTimer = null;
            }
        }

        private void ShowDelayedMessage()
        {
            if (_delayedMessage != null) return; // Already shown
            
            // Create delayed message text
            var messageText = new TextBlock
            {
                Text = "This could take a minute...",
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            
            // Create container for the message
            var messageContainer = new Border
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = messageText
            };
            
            _delayedMessage = messageContainer;
            
            // Add to chat panel
            ChatMessagesPanel.Children.Add(messageContainer);
            ChatScrollViewer.ScrollToEnd();
        }
    }
}