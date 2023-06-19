using System;
using System.Net.Http;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using KickDownloader.Properties;

namespace KickDownloader
{
    public partial class Form1 : Form
    {
        static readonly HttpClient client = new HttpClient();
        public KickVideoResponse CurrentVideo { get; set; }
        public KickClipResponse CurrentClip { get; set; }

        public Form1()
        {
            InitializeComponent();
        }

        private async void btnInfo_Click(object sender, EventArgs e)
        {
            string videoUrl = txtUrl.Text;

            if (String.IsNullOrWhiteSpace(videoUrl))
            {
                return;
            }

            string clipPattern = @"https://kick\.com/.*\?clip=(?<clipid>\d+)";
            Regex clipRegex = new Regex(clipPattern);
            string videoPattern = @"https://kick\.com/video/(?<uuid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})";
            Regex videoRegex = new Regex(videoPattern);

            Match videoMatch = videoRegex.Match(videoUrl);
            Match clipMatch = clipRegex.Match(videoUrl);

            try
            {
                if (videoMatch.Success)
                {
                    Group group = videoMatch.Groups["uuid"];
                    string videoId = group.Value;

                    btnInfo.Enabled = false;

                    KickVideoResponse videoInfo = await KickHelper.FetchKickVideoResponseAsync(videoId);
                    lblTitle.Text = videoInfo.title;
                    lblCreated.Text = videoInfo.created_at;

                    comboQuality.Enabled = true;
                    comboQuality.Items.Clear();
                    foreach (var stream in videoInfo.streams)
                    {
                        comboQuality.Items.Add(stream);
                    }
                    comboQuality.SelectedItem = videoInfo.streams[0];

                    btnInfo.Enabled = true;
                    btnDownload.Enabled = true;
                    CurrentVideo = videoInfo;
                    CurrentClip = null;
                }
                else if (clipMatch.Success)
                {
                    Group group = clipMatch.Groups["clipid"];
                    string clipId = group.Value;

                    btnInfo.Enabled = false;

                    KickClipResponse videoInfo = await KickHelper.FetchKickClipResponseAsync(clipId);
                    lblTitle.Text = videoInfo.title;
                    lblCreated.Text = videoInfo.created_at;

                    comboQuality.Enabled = false;
                    comboQuality.Items.Clear();

                    btnInfo.Enabled = true;
                    btnDownload.Enabled = true;
                    CurrentVideo = null;
                    CurrentClip = videoInfo;
                }
                else
                {
                    MessageBox.Show("Unable to parse link", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                btnInfo.Enabled = true;
                btnDownload.Enabled = false;
                comboQuality.Items.Clear();
                lblTitle.Text = "";
                lblCreated.Text = "";
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "MP4 File|*.mp4";
            saveFileDialog1.Title = "Save Video File";
            saveFileDialog1.InitialDirectory = Properties.Settings.Default.DefaultPath;
            saveFileDialog1.ShowDialog();

            string kickDir = Path.Combine(Path.GetTempPath(), "KickDownloader");
            if (!Directory.Exists(kickDir))
                Directory.CreateDirectory(kickDir);

            if (saveFileDialog1.FileName != "")
            {
                Properties.Settings.Default.DefaultPath = Path.GetDirectoryName(saveFileDialog1.FileName);
                Settings.Default.Save();

                if (CurrentVideo != null)
                {
                    try
                    {
                        btnDownload.Enabled = false;
                        btnInfo.Enabled = false;
                        lblStatus.Text = "Downloading";

                        Stream currentStream = (Stream)comboQuality.SelectedItem;
                        List<string> videoPartsList = await KickHelper.GetVideoParts(currentStream.source);

                        string tempDir = Path.Combine(kickDir, Guid.NewGuid().ToString());
                        if (!Directory.Exists(tempDir))
                            Directory.CreateDirectory(tempDir);

                        progressDownload.Value = 0;
                        progressDownload.Maximum = videoPartsList.Count;

                        using (var threadThrottler = new SemaphoreSlim(10))
                        {
                            Task[] downloadTasks = videoPartsList.Select(request => Task.Run(async () =>
                            {
                                await threadThrottler.WaitAsync();
                                try
                                {
                                    string tempFile = Path.Combine(tempDir, Path.GetFileName(request));
                                    await DownloadFileTaskAsync(request, tempFile);
                                    progressDownload.Invoke((MethodInvoker)delegate
                                    {
                                        progressDownload.PerformStep();
                                    });
                                }
                                finally
                                {
                                    threadThrottler.Release();
                                }
                            })).ToArray();
                            await Task.WhenAll(downloadTasks);
                        }

                        lblStatus.Text = "Combining Files";

                        string combinedFile = Path.Combine(tempDir, "final.ts");
                        await using var outputStream = new FileStream(combinedFile, FileMode.Create, FileAccess.Write, FileShare.None);
                        foreach (var videoFile in videoPartsList)
                        {
                            string filePath = Path.Combine(tempDir, Path.GetFileName(videoFile));
                            await using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                await fs.CopyToAsync(outputStream);
                            }
                        }
                        outputStream.Dispose();

                        lblStatus.Text = "Remuxing to MP4";

                        await Task.Run(() =>
                        {
                            var process = new Process
                            {
                                StartInfo =
                            {
                                FileName = "ffmpeg.exe",
                                Arguments = string.Format(
                                    "-avoid_negative_ts make_zero -i \"{0}\" -analyzeduration {1} -probesize {1} -c:v copy \"{2}\"",
                                    combinedFile, int.MaxValue, saveFileDialog1.FileName),
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardInput = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            }
                            };

                            process.Start();
                            process.WaitForExit();
                        });

                        lblStatus.Text = "Cleaning Up";

                        try
                        {
                            await Task.Run(() => { Directory.Delete(tempDir, true); });
                        }
                        catch { }

                        lblStatus.Text = "Done";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "Error";
                    }
                    finally
                    {
                        btnDownload.Enabled = true;
                        btnInfo.Enabled = true;
                    }
                }
                else if (CurrentClip != null)
                {
                    lblStatus.Text = "Downloading";
                    await DownloadFileTaskAsync(CurrentClip.video_url, saveFileDialog1.FileName);
                    lblStatus.Text = "Done";
                }
            }
        }

        private async Task DownloadFileTaskAsync(string url, string destinationFile)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            await response.Content.CopyToAsync(fs).ConfigureAwait(false);
        }
    }
}