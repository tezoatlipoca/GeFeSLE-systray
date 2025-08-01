using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GeFeSLE.Controls
{
    public class RichHtmlControl : UserControl
    {
        public static readonly StyledProperty<string> HtmlContentProperty =
            AvaloniaProperty.Register<RichHtmlControl, string>(nameof(HtmlContent), string.Empty);

        private StackPanel? _contentPanel;

        public string HtmlContent
        {
            get => GetValue(HtmlContentProperty);
            set => SetValue(HtmlContentProperty, value);
        }

        public RichHtmlControl()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == HtmlContentProperty)
            {
                UpdateContent();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // Simple approach - just let the base handle measurement with ClipToBounds=true
            var result = base.MeasureOverride(availableSize);
            
            // Ensure we never exceed the available width
            if (!double.IsInfinity(availableSize.Width))
            {
                result = result.WithWidth(Math.Min(result.Width, availableSize.Width));
            }
            
            return result;
        }

        private void InitializeComponent()
        {
            // Create the main layout with proper width constraints
            _contentPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 8
            };

            // No additional ScrollViewer here since the parent XAML already has one
            Content = _contentPanel;

            // Ensure the control respects container width and doesn't overflow
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            
            // Ensure we don't exceed container bounds
            ClipToBounds = true;
        }

        private void UpdateContent()
        {
            if (_contentPanel == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                _contentPanel.Children.Clear();

                if (string.IsNullOrWhiteSpace(HtmlContent))
                    return;

                try
                {
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(HtmlContent);

                    ProcessHtmlNode(htmlDoc.DocumentNode, _contentPanel);
                }
                catch (Exception ex)
                {
                    // Fallback to plain text if HTML parsing fails
                    var errorText = new TextBlock
                    {
                        Text = $"Error parsing HTML: {ex.Message}\n\nRaw content:\n{HtmlContent}",
                        Foreground = Brushes.Red,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    _contentPanel.Children.Add(errorText);
                }
            });
        }

        private void ProcessHtmlNode(HtmlNode node, Panel container)
        {
            foreach (var child in node.ChildNodes)
            {
                switch (child.NodeType)
                {
                    case HtmlNodeType.Text:
                        var text = child.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            ProcessTextWithImages(container, text);
                        }
                        break;

                    case HtmlNodeType.Element:
                        ProcessHtmlElement(child, container);
                        break;
                }
            }
        }

        private void ProcessHtmlElement(HtmlNode element, Panel container)
        {
            switch (element.Name.ToLower())
            {
                case "p":
                    var paragraph = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8),
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        MaxWidth = double.PositiveInfinity
                    };
                    container.Children.Add(paragraph);
                    break;

                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    var heading = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        FontWeight = FontWeight.Bold,
                        FontSize = GetHeadingSize(element.Name),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 8, 0, 8),
                        Foreground = Brushes.LightBlue,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    container.Children.Add(heading);
                    break;

                case "strong":
                case "b":
                    var bold = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    container.Children.Add(bold);
                    break;

                case "em":
                case "i":
                    var italic = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        FontStyle = FontStyle.Italic,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    container.Children.Add(italic);
                    break;

                case "a":
                    var href = element.GetAttributeValue("href", "");
                    var linkText = element.InnerText?.Trim() ?? href;
                    
                    var link = new Button
                    {
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = Brushes.LightBlue,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        Padding = new Thickness(0),
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                        Content = new TextBlock
                        {
                            Text = linkText,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.LightBlue,
                            TextDecorations = TextDecorations.Underline,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                        }
                    };
                    
                    if (!string.IsNullOrEmpty(href))
                    {
                        link.Click += (s, e) => OpenUrl(href);
                    }
                    
                    container.Children.Add(link);
                    break;

                case "ul":
                    var ulPanel = new StackPanel 
                    { 
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        Margin = new Thickness(20, 4, 0, 4),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    ProcessListItems(element, ulPanel, "• ");
                    container.Children.Add(ulPanel);
                    break;

                case "ol":
                    var olPanel = new StackPanel 
                    { 
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        Margin = new Thickness(20, 4, 0, 4),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    ProcessOrderedListItems(element, olPanel);
                    container.Children.Add(olPanel);
                    break;

                case "code":
                    var code = new TextBlock
                    {
                        Text = element.InnerText ?? "",
                        FontFamily = new FontFamily("Consolas,Monaco,'Courier New',monospace"),
                        Background = Brushes.DarkGray,
                        Padding = new Thickness(4, 2),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        MaxWidth = double.PositiveInfinity
                    };
                    container.Children.Add(code);
                    break;

                case "pre":
                    var pre = new TextBlock
                    {
                        Text = element.InnerText ?? "",
                        FontFamily = new FontFamily("Consolas,Monaco,'Courier New',monospace"),
                        Background = Brushes.DarkGray,
                        Padding = new Thickness(8),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 4),
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        MaxWidth = double.PositiveInfinity
                    };
                    container.Children.Add(pre);
                    break;

                case "br":
                    container.Children.Add(new TextBlock { Height = 8 });
                    break;

                case "img":
                    var imgSrc = element.GetAttributeValue("src", "");
                    var imgAlt = element.GetAttributeValue("alt", "Image");
                    
                    if (!string.IsNullOrEmpty(imgSrc))
                    {
                        CreateImageControl(container, imgSrc, imgAlt);
                    }
                    break;

                case "div":
                    var divPanel = new StackPanel 
                    { 
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    ProcessHtmlNode(element, divPanel);
                    container.Children.Add(divPanel);
                    break;

                default:
                    // For unknown elements, just process their children
                    ProcessHtmlNode(element, container);
                    break;
            }
        }

        private void ProcessListItems(HtmlNode listElement, Panel container, string bullet)
        {
            foreach (var item in listElement.Elements("li"))
            {
                var itemPanel = new StackPanel 
                { 
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 2),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                var bulletText = new TextBlock
                {
                    Text = bullet,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var itemContent = new TextBlock
                {
                    Text = item.InnerText?.Trim() ?? "",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    MaxWidth = double.PositiveInfinity
                };

                itemPanel.Children.Add(bulletText);
                itemPanel.Children.Add(itemContent);
                container.Children.Add(itemPanel);
            }
        }

        private void ProcessOrderedListItems(HtmlNode listElement, Panel container)
        {
            int index = 1;
            foreach (var item in listElement.Elements("li"))
            {
                var itemPanel = new StackPanel 
                { 
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 2),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                var numberText = new TextBlock
                {
                    Text = $"{index}. ",
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var itemContent = new TextBlock
                {
                    Text = item.InnerText?.Trim() ?? "",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    MaxWidth = double.PositiveInfinity
                };

                itemPanel.Children.Add(numberText);
                itemPanel.Children.Add(itemContent);
                container.Children.Add(itemPanel);
                index++;
            }
        }

        private double GetHeadingSize(string tagName)
        {
            return tagName.ToLower() switch
            {
                "h1" => 20,
                "h2" => 18,
                "h3" => 16,
                "h4" => 14,
                "h5" => 12,
                "h6" => 11,
                _ => 12
            };
        }

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log error or show message to user
                Console.WriteLine($"Failed to open URL {url}: {ex.Message}");
            }
        }

        private void CreateImageControl(Panel container, string imageUrl, string altText)
        {
            var imageContainer = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Margin = new Thickness(0, 8, 0, 8),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                MinHeight = 24 // Reserve space to prevent layout jumping
            };

            // Create image control (hidden initially)
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                MaxHeight = 300,
                Margin = new Thickness(0, 0, 0, 4),
                IsVisible = false // Hidden until loaded
            };

            // Create a stable placeholder that maintains consistent height
            var placeholderPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                MinHeight = 20,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };

            var loadingIcon = new TextBlock
            {
                Text = "⏳",
                FontSize = 14,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var loadingText = new TextBlock
            {
                Text = $"Loading: {(string.IsNullOrEmpty(altText) || altText == "Image" ? "image" : altText)}",
                Foreground = Brushes.Gray,
                FontSize = 11,
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };

            placeholderPanel.Children.Add(loadingIcon);
            placeholderPanel.Children.Add(loadingText);

            // Add both placeholder and image to container
            imageContainer.Children.Add(placeholderPanel);
            imageContainer.Children.Add(image);

            // Start loading immediately but don't block UI
            LoadImageAsync(image, imageUrl, altText, imageContainer, placeholderPanel);

            container.Children.Add(imageContainer);
        }

        private async void LoadImageAsync(Image imageControl, string imageUrl, string altText, Panel imageContainer, Panel placeholderPanel)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10); // Reasonable timeout

                var response = await httpClient.GetAsync(imageUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            using var stream = new System.IO.MemoryStream(imageBytes);
                            var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                            
                            imageControl.Source = bitmap;
                            
                            // Hide placeholder and show image
                            placeholderPanel.IsVisible = false;
                            imageControl.IsVisible = true;
                            
                            // Add alt text as caption if provided
                            if (!string.IsNullOrEmpty(altText) && altText != "Image")
                            {
                                var caption = new TextBlock
                                {
                                    Text = altText,
                                    Foreground = Brushes.LightGray,
                                    FontSize = 10,
                                    FontStyle = FontStyle.Italic,
                                    TextWrapping = TextWrapping.Wrap,
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                                    Margin = new Thickness(0, 2, 0, 0)
                                };
                                imageContainer.Children.Add(caption);
                            }
                        }
                        catch (Exception)
                        {
                            ShowBrokenImage(imageContainer, placeholderPanel, imageUrl, altText, $"Failed to load image: decode error");
                        }
                    });
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ShowBrokenImage(imageContainer, placeholderPanel, imageUrl, altText, $"Failed to load image: {(int)response.StatusCode} {response.ReasonPhrase}");
                    });
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowBrokenImage(imageContainer, placeholderPanel, imageUrl, altText, $"Failed to load image: network error");
                });
            }
            catch (TaskCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowBrokenImage(imageContainer, placeholderPanel, imageUrl, altText, $"Failed to load image: timeout");
                });
            }
            catch (Exception)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowBrokenImage(imageContainer, placeholderPanel, imageUrl, altText, $"Failed to load image: error");
                });
            }
        }

        private void ShowBrokenImage(Panel imageContainer, Panel placeholderPanel, string imageUrl, string altText, string errorMessage)
        {
            // Hide the loading placeholder
            placeholderPanel.IsVisible = false;
            
            // Create broken image indicator
            var brokenImagePanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 4),
                MinHeight = 20 // Maintain consistent height
            };

            // Broken image icon (using Unicode)
            var brokenIcon = new TextBlock
            {
                Text = "🖼️",
                FontSize = 16,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Image info
            var imageInfo = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical
            };

            var brokenText = new TextBlock
            {
                Text = $"[Broken Image: {altText}]",
                Foreground = Brushes.Orange,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11
            };

            var urlText = new Button
            {
                Content = imageUrl.Length > 50 ? imageUrl.Substring(0, 47) + "..." : imageUrl,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.LightBlue,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                FontSize = 10
            };

            urlText.Click += (s, e) => OpenUrl(imageUrl);

            var errorText = new TextBlock
            {
                Text = errorMessage,
                Foreground = Brushes.Gray,
                FontSize = 9,
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };

            imageInfo.Children.Add(brokenText);
            imageInfo.Children.Add(urlText);
            imageInfo.Children.Add(errorText);

            brokenImagePanel.Children.Add(brokenIcon);
            brokenImagePanel.Children.Add(imageInfo);

            // Replace the placeholder with the broken image display
            var placeholderIndex = imageContainer.Children.IndexOf(placeholderPanel);
            if (placeholderIndex >= 0)
            {
                imageContainer.Children[placeholderIndex] = brokenImagePanel;
            }
            else
            {
                imageContainer.Children.Add(brokenImagePanel);
            }
        }

        private void ProcessTextWithImages(Panel container, string text)
        {
            // Regex to match Markdown-style images: ![alt](url)
            var imageRegex = new System.Text.RegularExpressions.Regex(@"!\[([^\]]*)\]\(([^)]+)\)");
            var matches = imageRegex.Matches(text);

            if (matches.Count == 0)
            {
                // No images found, just add regular text
                var textBlock = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    MaxWidth = double.PositiveInfinity
                };
                container.Children.Add(textBlock);
                return;
            }

            // Process text with images
            int lastIndex = 0;
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Add text before this image (if any)
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeText))
                    {
                        var textBlock = new TextBlock
                        {
                            Text = beforeText,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.White,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            MaxWidth = double.PositiveInfinity
                        };
                        container.Children.Add(textBlock);
                    }
                }

                // Add the image
                var altText = match.Groups[1].Value;
                var imageUrl = match.Groups[2].Value;
                
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    CreateImageControl(container, imageUrl, altText);
                }

                lastIndex = match.Index + match.Length;
            }

            // Add any remaining text after the last image
            if (lastIndex < text.Length)
            {
                var afterText = text.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(afterText))
                {
                    var textBlock = new TextBlock
                    {
                        Text = afterText,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        MaxWidth = double.PositiveInfinity
                    };
                    container.Children.Add(textBlock);
                }
            }
        }
    }
}
