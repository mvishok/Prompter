using Newtonsoft.Json;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http; // Required for making HTTP requests
using System.Security.Cryptography;
using System.Text.RegularExpressions; // Required for extracting title from HTML
using System.Threading.Tasks;
using System.Web; // Required for parsing query strings
using System.Windows.Forms;

namespace Prompter
{
    public partial class Form1 : Form
    {
        private readonly string receivedUrl;
        private string referrerUrl;
        private string referrerDomain;
        private string uid;
        private string endpoint; // New variable for endpoint
        private const string subscriptionFile = "subscriptions.txt"; // File to store subscriptions
        private readonly string exePath;
        private bool isUnsubscribe = false; // Flag to check if it's an unsubscribe request

        public Form1(string[] args)
        {
            // Initialize form components
            InitializeComponent();

            // Make form invisible initially
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();

            // Get the path where the EXE is located
            exePath = AppDomain.CurrentDomain.BaseDirectory;

            // Ensure the subscription file exists
            EnsureSubscriptionFile();

            // Check if URL arguments were passed
            if (args.Length > 0)
            {
                receivedUrl = args[0]; // Get the URL from the arguments
                Parse(receivedUrl); // Parse the URL
            }
            else
            {
                MessageBox.Show("No URL was passed to the application", "Prompter Receiver", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        private void Parse(string url)
        {
            try
            {
                // Check if the URL is for unsubscribing
                if (url.StartsWith("prompter://unsubscribe"))
                {
                    isUnsubscribe = true;
                    Uri uri = new Uri(url);
                    NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

                    // Get referrer, UID, and endpoint from the query parameters
                    referrerUrl = query["referrer"];
                    uid = query["uid"];
                    endpoint = query["endpoint"]; // Get the endpoint

                    if (referrerUrl == null || uid == null || endpoint == null)
                    {
                        throw new Exception("Referrer, UID, or endpoint not found in URL.");
                    }

                    referrerDomain = GetDomain(referrerUrl);
                    ValidateEndpoint(endpoint, referrerDomain); // Validate endpoint
                }
                else
                {
                    // Normal subscription flow
                    Uri uri = new Uri(url);
                    NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

                    // Get referrer, UID, and endpoint from the query parameters
                    referrerUrl = query["referrer"];
                    uid = query["uid"];
                    endpoint = query["endpoint"]; // Get the endpoint

                    if (referrerUrl == null || uid == null || endpoint == null)
                    {
                        throw new Exception("Referrer, UID, or endpoint not found in URL.");
                    }

                    referrerDomain = GetDomain(referrerUrl);
                    ValidateEndpoint(endpoint, referrerDomain); // Validate endpoint

                    // Set the form's title to the domain's title (ASYNC call)
                    SetFormTitle(referrerDomain); // <-- Ensure this line is in place
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error parsing URL: " + e.Message, "Prompter Receiver", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        private void ValidateEndpoint(string endpoint, string referrerDomain)
        {
            // Check if the endpoint is a subdomain or subpath of the referrer domain
            if (!IsEndpointRelatedToReferrer(endpoint, referrerDomain))
            {
                throw new Exception($"Endpoint '{endpoint}' is not related to referrer '{referrerDomain}'.");
            }
        }

        private bool IsEndpointRelatedToReferrer(string endpoint, string referrerDomain)
        {
            // Basic validation: Check if the endpoint contains the referrer domain
            return endpoint.Contains(referrerDomain) || endpoint.Equals(referrerDomain, StringComparison.OrdinalIgnoreCase);
        }

        private async void SetFormTitle(string domain)
        {
            // Fetch the title of the domain asynchronously
            string title = await GetWebsiteTitle(domain);
            this.Text = string.IsNullOrEmpty(title) ? domain : title;
        }

        private async Task<string> GetWebsiteTitle(string domain)
        {
            try
            {
                // Make an HTTP request to the referrer URL to get the page's HTML
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync($"https://{domain}");
                    response.EnsureSuccessStatusCode();
                    string pageContent = await response.Content.ReadAsStringAsync();

                    // Extract the <title> tag from the HTML
                    Match match = Regex.Match(pageContent, @"<title>\s*(.+?)\s*</title>", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch (Exception)
            {
                return null;                
            }
            return null; // Return null if the title cannot be fetched
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Show form after processing the URL
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Show();
            this.Activate();
            this.BringToFront();

            // Label to show domain and ask if user wants to subscribe or unsubscribe
            infoLabel.Text = referrerDomain;

            if (isUnsubscribe)
            {
                label1.Text = "Would you like to unsubscribe notifications from";
                yesButton.Click += new EventHandler(UnsubscribeButton_Click);
            }
            else
            {
                yesButton.Text = "YES";
                yesButton.Click += new EventHandler(YesButton_Click);
            }

            noButton.Click += new EventHandler(NoButton_Click);
        }

        private async void YesButton_Click(object sender, EventArgs e)
        {
            yesButton.Enabled = false; // Disable the button to prevent multiple clicks
            noButton.Enabled = false; // Disable the button to prevent multiple clicks
            try
            {
                string filePath = Path.Combine(exePath, subscriptionFile);
                bool subscriptionExists = false;
                string oldUid = null;

                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        foreach (var line in lines)
                        {
                            if (line.Contains($"{endpoint}||{uid}")) // Check for existing subscription
                            {
                                // Get the old UID from the line
                                string[] parts = line.Split(new[] { "||" }, StringSplitOptions.None);
                                oldUid = parts.Length > 1 ? parts[1] : null; // Assuming UID is the second part

                                // Update the existing subscription by overwriting the line
                                writer.WriteLine($"{endpoint}||{uid}"); // Write new UID
                                subscriptionExists = true;
                            }
                            else
                            {
                                // Keep existing subscriptions
                                writer.WriteLine(line);
                            }
                        }

                        // If no subscription was found, add a new one
                        if (!subscriptionExists)
                        {
                            writer.WriteLine($"{endpoint}||{uid}"); // Write new subscription
                        }
                    }
                }
                else
                {
                    // If the file doesn't exist, create it and add the new subscription
                    using (StreamWriter writer = new StreamWriter(filePath, true))
                    {
                        writer.WriteLine($"{endpoint}||{uid}"); // Write new subscription
                    }
                }

                // If updating, send request to updateUid endpoint
                if (subscriptionExists && oldUid != null)
                {
                    string updateUrl = $"{endpoint}/updateUid?old={oldUid}&new={uid}";

                    using (HttpClient client = new HttpClient())
                    {
                        HttpResponseMessage response = await client.GetAsync(updateUrl);

                        // Check the response status
                        if (response.IsSuccessStatusCode)
                        {
                            string jsonResponse = await response.Content.ReadAsStringAsync();
                            dynamic json = JsonConvert.DeserializeObject(jsonResponse);

                            // Check if the status is success
                            if (json.status == "success")
                            {
                                MessageBox.Show($"Successfully updated subscription for {endpoint}.", "Subscribed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                // Show error message if status is not success
                                MessageBox.Show($"Error: {json.error}", "Subscription Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Application.Exit();
                            }
                        }
                        else
                        {
                            // Show error message if response is not 200 OK
                            MessageBox.Show($"Error: Received {response.StatusCode} from the server.", "HTTP Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                        }
                    }
                }
                else
                {
                    // send request to subscribe endpoint
                    string subscribeUrl = $"{endpoint}/subscribe?uid={uid}";

                    using (HttpClient client = new HttpClient())
                    {
                        HttpResponseMessage response = await client.GetAsync(subscribeUrl);

                        // Check the response status
                        if (response.IsSuccessStatusCode)
                        {
                            string jsonResponse = await response.Content.ReadAsStringAsync();
                            dynamic json = JsonConvert.DeserializeObject(jsonResponse);

                            // Check if the status is success
                            if (json.status == "success")
                            {
                                MessageBox.Show($"Successfully subscribed to {endpoint}.", "Subscribed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                // Show error message if status is not success
                                MessageBox.Show($"Error: {json.error}", "Subscription Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Application.Exit();
                            }
                        }
                        else
                        {
                            // Show error message if response is not 200 OK
                            MessageBox.Show($"Error: Received {response.StatusCode} from the server.", "HTTP Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving subscription: " + ex.Message, "Prompter Receiver", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Exit the application after subscribing or updating
                Environment.Exit(0);
            }
        }

        private void NoButton_Click(object sender, EventArgs e)
        {
            // Simply exit if user declines to subscribe or unsubscribe
            Environment.Exit(0);
        }

        private void UnsubscribeButton_Click(object sender, EventArgs e)
        {
            //Disable the buttons to prevent multiple clicks
            yesButton.Enabled = false;
            noButton.Enabled = false;

            // Remove the subscription for the endpoint
            try
            {
                string filePath = Path.Combine(exePath, subscriptionFile);
                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);
                    bool subscriptionFound = false; // Flag to check if subscription was found
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        foreach (var line in lines)
                        {
                            if (!line.Contains($"{endpoint}||{uid}")) // Check against endpoint
                            {
                                writer.WriteLine(line); // Rewrite all except the unsubscribed endpoint
                            }
                            else
                            {
                                subscriptionFound = true; // Found the subscription to unsubscribe
                            }
                        }
                    }

                    if (subscriptionFound)
                    {

                        //send request to unsubscribe endpoint
                        string unsubscribeUrl = $"{endpoint}/unsubscribe?uid={uid}";
                        
                        using (HttpClient client = new HttpClient())
                        {
                            HttpResponseMessage response = client.GetAsync(unsubscribeUrl).Result;

                            // Check the response status
                            if (response.IsSuccessStatusCode)
                            {
                                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                                dynamic json = JsonConvert.DeserializeObject(jsonResponse);

                                // Check if the status is success
                                if (json.status == "success")
                                {
                                    MessageBox.Show($"Successfully unsubscribed from {endpoint}.", "Unsubscribed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    // Show error message if status is not success
                                    MessageBox.Show($"Error: {json.error}", "Unsubscribe Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            else
                            {
                                // Show error message if response is not 200 OK
                                MessageBox.Show($"Error: Received {response.StatusCode} from the server.", "HTTP Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show($"No subscription found for {endpoint}.", "Unsubscribe Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show($"No subscriptions found.", "Unsubscribe Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error removing subscription: " + ex.Message, "Prompter Receiver", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Exit the application after unsubscribing
            Environment.Exit(0);
        }

        private void EnsureSubscriptionFile()
        {
            string filePath = Path.Combine(exePath, subscriptionFile);
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close(); // Create the file if it doesn't exist
            }
        }

        private string GetDomain(string url)
        {
            // Extract the domain from the URL
            Uri uri = new Uri(url);
            return uri.Host; // Return the host part of the URL
        }
    }
}
