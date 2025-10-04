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

                // Get AI response with thinking process
                var response = await _groqService.SendMessageAsync(messageText, null);

                // Add final AI response - appears instantly
                AddAiResponseWithThinking(response, false);
                
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

        private void AddAiResponseWithThinking(Services.AiResponse response, bool animate = true)
        {
            var container = new Border
            {
                Style = (Style)FindResource("MessageContainerStyle")
            };

            var stackPanel = new StackPanel();

            // Only show the conclusion, exclude thinking process
            var displayText = !string.IsNullOrEmpty(response.Conclusion) ? response.Conclusion : "No response received";

            // Add simple plain text block - no formatting
            var responseText = new TextBlock
            {
                Text = displayText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F1F1F")),
                LineHeight = 20,
                Margin = new Thickness(0)
            };

            stackPanel.Children.Add(responseText);
            
            // Set container child and add to panel once, after all elements are added
            container.Child = stackPanel;
            ChatMessagesPanel.Children.Add(container);
            ChatScrollViewer.ScrollToEnd();
        }

        private void AddAiResponse(string text)
        {
            // Fallback method for simple text responses
            var response = new Services.AiResponse { Conclusion = text };
            AddAiResponseWithThinking(response, false);
        }




        




    }
}