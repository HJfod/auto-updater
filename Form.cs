﻿using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace auto_updater {
    public partial class Pog : Form {
        public string Repo;
        public string AppVersion;
        public string AppName;
        public string ExeName;
        public float DPI = 1F;
        public TableLayoutPanel container;

        private int ConvertVer(string ver) {
            string res = "";
            foreach (Match m in Regex.Matches(ver, "[0-9]+", RegexOptions.None, Regex.InfiniteMatchTimeout))
                res += m.Value;
            return Int32.Parse(res);
        }

        private Control AddLoad(string _text, Control h) {
            TableLayoutPanel c = new TableLayoutPanel();
            c.Dock = DockStyle.Fill;
            c.Height = 30;
            c.ColumnCount = 2;
            c.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            c.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            Label text = new Label();
            text.AutoSize = true;
            text.Text = _text;
            text.Name = "__text";

            ProgressBar p = new ProgressBar();
            p.Maximum = 100;
            p.Minimum = 0;
            p.Step = 1;
            p.Value = 1;
            p.Width = c.Width / 2;
            p.Name = "__pogg";

            c.Controls.Add(text, 0, 0);
            c.Controls.Add(p, 1, 0);

            h.Controls.Add(c);
            
            return c;
        }

        private string GetJSONKey(string _json, string _key) {
            string tag = Regex.Match(_json, $"\"{_key}\":\".*?\"", RegexOptions.None, Regex.InfiniteMatchTimeout).Value;
            if (tag == null || tag == "") return null;
            tag = tag.Substring(tag.IndexOf(":"));
            tag = tag.Substring(tag.IndexOf('"') + 1, tag.LastIndexOf('"') - tag.IndexOf('"') - 1);
            return tag;
        }

        private Control _G(Control c, string type) {
            if (type == "p") {
                return ((ProgressBar)c.Controls.Find("__pogg", true)[0]);
            } else {
                return c.Controls.Find("__text", true)[0];
            }
        }

        public Pog() {
            this.DPI = ( (new Label()).CreateGraphics().DpiX / 96 );

            this.Text = "Auto-update";
            this.Size = new Size((int)(400F * DPI), (int)(300F * DPI));

            this.CenterToScreen();

            foreach (string line in File.ReadAllLines(".update")) {
                string val = line.Substring(line.IndexOf(":") + 1).Trim();

                switch (line.Substring(0, line.IndexOf(":")).Trim()) {
                    case "app-name":
                        this.Text = $"Updating {val}...";
                        this.AppName = val;
                        break;
                    case "github-repo":
                        this.Repo = val;
                        break;
                    case "app-version":
                        this.AppVersion = val;
                        break;
                    case "run-exe":
                        this.ExeName = val;
                        break;
                }
            }
            
            if (this.Repo == null) {
                MessageBox.Show("Github repository not defined!", "Error");
                this.Dispose();
            }
            if (this.AppVersion == null) {
                MessageBox.Show("Current app version not defined!", "Error");
                this.Dispose();
            }

            container = new TableLayoutPanel();
            container.Dock = DockStyle.Fill;
            container.AutoSize = true;

            var vercheck = AddLoad("Checking version...", container);

            this.Controls.Add(container);

            this.Show();
            
            try {
                string url = $"https://api.github.com/repos/{this.Repo}/releases/latest";

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "request";

                HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                if (res.StatusCode != HttpStatusCode.OK)
                    throw new Exception($"Can't connect to Github! {res.StatusCode}");

                Stream resStream = res.GetResponseStream();
                StreamReader streamRead = new StreamReader( resStream );

                string rec = "";

                Char[] buffer = new Char[256];
                int count = streamRead.Read( buffer, 0, 256 );

                while (count > 0) {
                    rec += new String(buffer, 0, count);
                    count = streamRead.Read(buffer, 0, 256);
                }

                streamRead.Close();
                resStream.Close();
                res.Close();
                
                int vern = ConvertVer(GetJSONKey(rec, "tag_name"));
                int vero = ConvertVer(this.AppVersion);
                if (vero == vern) {
                    this._G(vercheck, "t").Text = "Already up-to-date.";
                    ((ProgressBar)this._G(vercheck, "p")).Value = 100;
                } else if (vero < vern) {
                    this._G(vercheck, "t").Text = $"Target version: {GetJSONKey(rec, "tag_name")}";
                    ((ProgressBar)this._G(vercheck, "p")).Value = 100;

                    var down = AddLoad("Downloading...", container);

                    WebClient cli = new WebClient();
                    cli.Headers.Add("Accept: text/html, application/xhtml+xml, */*");
                    cli.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                    cli.DownloadProgressChanged += (s, e) => {
                        double now = double.Parse(e.BytesReceived.ToString());
                        double total = double.Parse(e.TotalBytesToReceive.ToString());
                        double perc = now / total * 100;

                        this._G(down, "t").Text = $"Downloaded {Math.Floor(now / 1000000)}mb / {Math.Floor(total / 1000000)}mb ({Math.Floor(perc)}%)...";
                        ((ProgressBar)this._G(down, "p")).Value = int.Parse(Math.Truncate(perc).ToString());
                    };
                    cli.DownloadFileCompleted += (s, e) => {
                        this._G(down, "t").Text = "Download complete!";

                        var unzip = AddLoad("Unzipping...", container);

                        ZipFile.ExtractToDirectory("__download.zip", $"{Directory.GetCurrentDirectory()}", true);
                        File.Delete("__download.zip");

                        this._G(unzip, "t").Text = $"Succesfully unzipped!";
                        ((ProgressBar)this._G(unzip, "p")).Value = 100;

                        TableLayoutPanel p = new TableLayoutPanel();
                        p.AutoSize = true;

                        CheckBox en = new CheckBox();
                        en.Text = $"Launch {AppName}";
                        en.AutoSize = true;

                        Button fin = new Button();
                        fin.Text = "Finish";
                        fin.AutoSize = true;
                        fin.Click += (s, e) => {
                            if (en.Checked)
                                Process.Start($"{Directory.GetCurrentDirectory()}\\{this.ExeName}");
                            this.Dispose();
                        };

                        p.Controls.Add(en);
                        p.Controls.Add(fin);

                        container.Controls.Add(p);
                    };
                    Console.WriteLine(GetJSONKey(rec, "browser_download_url"));
                    cli.DownloadFileAsync(new Uri(GetJSONKey(rec, "browser_download_url")), "__download.zip");
                } else if (vero > vern) {
                    this._G(vercheck, "t").Text = "Using a dev version";
                    ((ProgressBar)this._G(vercheck, "p")).Value = 100;
                }
            } catch(Exception e) {
                MessageBox.Show(e.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
