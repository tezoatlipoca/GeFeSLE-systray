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
                        var text = child.InnerText;
                        if (!string.IsNullOrEmpty(text))
                        {
                            // Don't trim text nodes unless they're completely whitespace
                            // This preserves important spacing like around parentheses
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                continue; // Skip whitespace-only nodes
                            }
                            
                            // Check if we're in a code context by looking at parent elements
                            bool isInCodeContext = IsInCodeContext(child);
                            ProcessTextWithSpecialElements(container, text, isInCodeContext);
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
                    // Process paragraph content with special text processing
                    var paragraphPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        Margin = new Thickness(0, 0, 0, 8),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    
                    // Process the paragraph's child nodes (which includes text and other elements)
                    ProcessHtmlNode(element, paragraphPanel);
                    container.Children.Add(paragraphPanel);
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
                    var codeText = element.InnerText ?? "";
                    var code = new TextBlock
                    {
                        Text = codeText,
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
                    var preText = element.InnerText ?? "";
                    var pre = new TextBlock
                    {
                        Text = preText,
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
                    // Special handling for importattribution divs to prevent line breaks around parentheses
                    var divClass = element.GetAttributeValue("class", "");
                    if (divClass.Contains("importattribution"))
                    {
                        // Process the entire text content as one unit to preserve formatting
                        var fullText = element.InnerText;
                        if (!string.IsNullOrWhiteSpace(fullText))
                        {
                            bool isInCodeContext = IsInCodeContext(element);
                            ProcessTextWithSpecialElements(container, fullText, isInCodeContext);
                        }
                    }
                    else
                    {
                        var divPanel = new StackPanel 
                        { 
                            Orientation = Avalonia.Layout.Orientation.Vertical,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                        };
                        ProcessHtmlNode(element, divPanel);
                        container.Children.Add(divPanel);
                    }
                    break;

                case "span":
                    // For spans, just process their child elements directly
                    ProcessHtmlNode(element, container);
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

                // Create a container for the item content that can handle mixed text and links
                var itemContentPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Vertical,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                // Process the list item's child nodes properly instead of using InnerText
                ProcessHtmlNode(item, itemContentPanel);

                itemPanel.Children.Add(bulletText);
                itemPanel.Children.Add(itemContentPanel);
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

                // Create a container for the item content that can handle mixed text and links
                var itemContentPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Vertical,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                // Process the list item's child nodes properly instead of using InnerText
                ProcessHtmlNode(item, itemContentPanel);

                itemPanel.Children.Add(numberText);
                itemPanel.Children.Add(itemContentPanel);
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
            // Process text for images, markdown links, and URLs
            ProcessTextWithSpecialElements(container, text, false);
        }

        private void ProcessTextWithSpecialElements(Panel container, string text, bool isInCodeContext)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var patterns = new List<(Regex regex, string type)>();
            
            // Markdown images: ![alt](url)
            patterns.Add((new Regex(@"!\[([^\]]*)\]\(([^)]+)\)"), "image"));
            
            // Don't process links in code contexts
            if (!isInCodeContext)
            {
                // Markdown links: [text](url)
                patterns.Add((new Regex(@"(?<!!)\[([^\]]+)\]\(([^)]+)\)"), "markdown-link"));
                
                // URLs in parentheses: (https://example.com)
                patterns.Add((new Regex(@"\(https?://[^\s<>\[\]()]+\)"), "url-in-parens"));
                
                // Regular URLs starting with http/https
                patterns.Add((new Regex(@"(?<!\]\()https?://[^\s<>\[\]()]*[^\s<>\[\]().,;!?]"), "url"));
                
                // Markdown bold: **text**
                patterns.Add((new Regex(@"\*\*([^*]+)\*\*"), "markdown-bold"));
                
                // Markdown italic: _text_
                patterns.Add((new Regex(@"(?<!\w)_([^_\s][^_]*[^_\s]|[^_\s])_(?!\w)"), "markdown-italic"));
            }

            // Find all matches and sort by position
            var allMatches = new List<(int start, int length, string type, Match match)>();
            
            foreach (var (regex, type) in patterns)
            {
                var matches = regex.Matches(text);
                foreach (Match match in matches)
                {
                    allMatches.Add((match.Index, match.Length, type, match));
                }
            }

            // Sort by position to process in order
            allMatches.Sort((a, b) => a.start.CompareTo(b.start));

            if (allMatches.Count == 0)
            {
                // No special elements found, just add regular text
                AddTextBlock(container, text);
                return;
            }

            // Create a WrapPanel to contain all text elements inline
            var wrapPanel = new WrapPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };

            // Process text with special elements
            int lastIndex = 0;
            
            foreach (var (start, length, type, match) in allMatches)
            {
                // Skip overlapping matches (e.g., URLs already inside markdown links)
                if (start < lastIndex)
                    continue;

                // Add text before this element (if any)
                if (start > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, start - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeText))
                    {
                        AddInlineTextBlock(wrapPanel, beforeText);
                    }
                }

                // Process the special element
                switch (type)
                {
                    case "image":
                        var altText = match.Groups[1].Value;
                        var imageUrl = match.Groups[2].Value;
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            CreateImageControl(container, imageUrl, altText);
                        }
                        break;

                    case "markdown-link":
                        var linkText = match.Groups[1].Value;
                        var linkUrl = match.Groups[2].Value;
                        if (!string.IsNullOrEmpty(linkUrl))
                        {
                            CreateInlineLinkButton(wrapPanel, linkUrl, linkText);
                        }
                        break;

                    case "url-in-parens":
                        var fullMatch = match.Value; // e.g., "(https://mas.to)"
                        var url = fullMatch.Substring(1, fullMatch.Length - 2); // Remove parentheses
                        // Add opening parenthesis as text
                        AddInlineTextBlock(wrapPanel, "(");
                        // Add URL as link
                        CreateInlineLinkButton(wrapPanel, url, url);
                        // Add closing parenthesis as text
                        AddInlineTextBlock(wrapPanel, ")");
                        break;

                    case "url":
                        var url2 = match.Value;
                        CreateInlineLinkButton(wrapPanel, url2, url2);
                        break;

                    case "markdown-bold":
                        var boldText = match.Groups[1].Value;
                        CreateInlineFormattedTextBlock(wrapPanel, boldText, FontWeight.Bold, FontStyle.Normal);
                        break;

                    case "markdown-italic":
                        var italicText = match.Groups[1].Value;
                        CreateInlineFormattedTextBlock(wrapPanel, italicText, FontWeight.Normal, FontStyle.Italic);
                        break;
                }

                lastIndex = start + length;
            }

            // Add any remaining text after the last element
            if (lastIndex < text.Length)
            {
                var afterText = text.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(afterText))
                {
                    AddInlineTextBlock(wrapPanel, afterText);
                }
            }

            // Only add the wrap panel if it has children
            if (wrapPanel.Children.Count > 0)
            {
                container.Children.Add(wrapPanel);
            }
        }

        private void AddTextBlock(Panel container, string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                MaxWidth = double.PositiveInfinity
            };
            container.Children.Add(textBlock);
        }

        private void CreateLinkButton(Panel container, string url, string displayText)
        {
            var link = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.LightBlue,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Margin = new Thickness(0, 0, 4, 0),
                Content = new TextBlock
                {
                    Text = displayText,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.LightBlue,
                    TextDecorations = TextDecorations.Underline
                }
            };
            
            link.Click += (s, e) => OpenUrl(url);
            container.Children.Add(link);
        }

        private void CreateFormattedTextBlock(Panel container, string text, FontWeight weight, FontStyle style)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontWeight = weight,
                FontStyle = style,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                MaxWidth = double.PositiveInfinity
            };
            container.Children.Add(textBlock);
        }

        private bool IsInCodeContext(HtmlNode node)
        {
            // Check if this text node is inside a code or pre element
            var parent = node.ParentNode;
            while (parent != null)
            {
                if (parent.Name?.ToLower() == "code" || parent.Name?.ToLower() == "pre")
                {
                    return true;
                }
                parent = parent.ParentNode;
            }
            return false;
        }

        private void AddInlineTextBlock(Panel container, string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            container.Children.Add(textBlock);
        }

        private void CreateInlineLinkButton(Panel container, string url, string displayText)
        {
            var link = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.LightBlue,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Content = new TextBlock
                {
                    Text = displayText,
                    TextWrapping = TextWrapping.NoWrap,
                    Foreground = Brushes.LightBlue,
                    TextDecorations = TextDecorations.Underline
                }
            };
            
            link.Click += (s, e) => OpenUrl(url);
            container.Children.Add(link);
        }

        private void CreateInlineFormattedTextBlock(Panel container, string text, FontWeight weight, FontStyle style)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontWeight = weight,
                FontStyle = style,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            container.Children.Add(textBlock);
        }
    }
}
