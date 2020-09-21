using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace auto_updater {
    public partial class Pog : Form {
        public string Repo;
        public string AppVersion;
        public ProgressBar poggers;
        public Label pogtext;

        private int ConvertVer(string ver) {
            string res = "";
            foreach (Match m in Regex.Matches(ver, "[0-9]+", RegexOptions.None, Regex.InfiniteMatchTimeout))
                res += m.Value;
            return Int32.Parse(res);
        }

        private string GetJSONKey(string _json, string _key) {
            string tag = Regex.Match(_json, $"\"{_key}\":\".*?\"", RegexOptions.None, Regex.InfiniteMatchTimeout).Value;
            if (tag == null || tag == "") return null;
            tag = tag.Substring(tag.IndexOf(":"));
            tag = tag.Substring(tag.IndexOf('"') + 1, tag.LastIndexOf('"') - 1);
            return tag;
        }

        public Pog() {
            this.Text = "Auto-update";
            this.Size = new Size(400, 200);

            foreach (string line in File.ReadAllLines(".update")) {
                string val = line.Substring(line.IndexOf(":") + 1).Trim();

                switch (line.Substring(0, line.IndexOf(":")).Trim()) {
                    case "app-name":
                        this.Text = val;
                        break;
                    case "github-repo":
                        this.Repo = val;
                        break;
                    case "app-version":
                        this.AppVersion = val;
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

            TableLayoutPanel container = new TableLayoutPanel();

            pogtext = new Label();
            pogtext.AutoSize = true;

            pogtext.Text = "Checking version...";

            poggers = new ProgressBar();
            poggers.Maximum = 100;
            poggers.Minimum = 0;
            poggers.Step = 1;
            poggers.Value = 1;

            container.Controls.Add(pogtext);
            container.Controls.Add(poggers);

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
                    this.Dispose();
                } else if (vero < vern) {
                    WebClient cli = new WebClient();
                    cli.Headers.Add("Accept: text/html, application/xhtml+xml, */*");
                    cli.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                    cli.DownloadProgressChanged += (s, e) => {
                        double now = double.Parse(e.BytesReceived.ToString());
                        double total = double.Parse(e.TotalBytesToReceive.ToString());
                        double perc = now / total * 100;

                        this.pogtext.Text = $"Downloaded {now} / {total} bytes...";
                        this.poggers.Value = int.Parse(Math.Truncate(perc).ToString());
                    };
                    cli.DownloadFileCompleted += (s, e) => {
                        this.pogtext.Text = "Download complete!";
                    };
                    Console.WriteLine(GetJSONKey(rec, "browser_download_url"));
                    cli.DownloadFileAsync(new Uri(GetJSONKey(rec, "browser_download_url")), "__download.zip");
                } else if (vero > vern) {
                    this.Dispose();
                }
            } catch(Exception e) {
                MessageBox.Show(e.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
