using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Management;

namespace tptBackup
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public List<string> usbDrives = new List<string>();
        public int debugSize = 406;
        public int normalSize = 219;
        public string extDrive = "";
        public string extDir = "\\Backup";
        public string sourceDir = "C:\\";
        public string logDir = Path.GetDirectoryName(Application.ExecutablePath) + "\\logs";
        public string cfgFile = Path.GetDirectoryName(Application.ExecutablePath) + "\\tptbackup.ini";
        public bool logging = true;

        private void Form1_Load(object sender, EventArgs e)
        {
            if (File.Exists(cfgFile))
            {
                textBox1.AppendText(getTs() + " " + "Reading config from: " + cfgFile + Environment.NewLine);
                var cfg = new IniFile(cfgFile);
                if (cfg.KeyExists("extDir", "tptBackup")) { extDir = (string)cfg.Read("extDir", "tptBackup"); }
                if (cfg.KeyExists("extDrive", "tptBackup")) { extDrive = (string)cfg.Read("extDrive", "tptBackup"); }
                if (cfg.KeyExists("sourceDir", "tptBackup")) { sourceDir = (string)cfg.Read("sourceDir", "tptBackup"); }
                if (cfg.KeyExists("logDir", "tptBackup")) { logDir = (string)cfg.Read("logDir", "tptBackup"); }
            }

            textBox1.AppendText(getTs() + "Source: " + sourceDir + Environment.NewLine);
            textBox1.AppendText(getTs() + "Target Dir: " + extDir + Environment.NewLine);

            if (!Directory.Exists(logDir))
            {
                try
                {
                    textBox1.AppendText(getTs() + " Log directory does not exist. Attempting to create." + Environment.NewLine);
                    DirectoryInfo di = Directory.CreateDirectory(logDir);
                }
                catch (Exception ex)
                {
                    logging = false;
                    textBox1.AppendText(getTs() + "Unable to create log directory. Logging is now disabled. " + logDir + Environment.NewLine);
                }
                finally
                {
                    textBox1.AppendText(getTs() + "Logging is enabled." + Environment.NewLine);
                }
            }
            Height = normalSize;
            comboBox1.Items.Clear();
            int i;
            if (extDrive != "")
            {
                usbDrives.Add(extDrive);
                comboBox1.Items.Add(extDrive + " (manually added)");
                textBox1.AppendText(getTs() + "Using manually specified drive" + Environment.NewLine);
                i = 1;
            }
            else { i = 0; }
            /*foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable)
                {
                    drive.VolumeLabel
                    comboBox1.Items.Add(drive.Name + " (" + drive.VolumeLabel + ")");
                    usbDrives.Add(drive.Name);
                    i++;
                }
            }*/
            foreach (ManagementObject device in new ManagementObjectSearcher(@"SELECT * FROM Win32_DiskDrive WHERE InterfaceType LIKE 'USB%'").Get())
            {
                Console.WriteLine((string)device.GetPropertyValue("DeviceID"));
                Console.WriteLine((string)device.GetPropertyValue("PNPDeviceID"));

                foreach (ManagementObject partition in new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + device.Properties["DeviceID"].Value
                    + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition").Get())
                {
                    foreach (ManagementObject disk in new ManagementObjectSearcher(
                                "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='"
                                    + partition["DeviceID"]
                                    + "'} WHERE AssocClass = Win32_LogicalDiskToPartition").Get())
                    {
                        /*foreach (PropertyData prop in disk.Properties)
                        {
                            textBox1.AppendText(getTs() + " " + prop.Name + ": " + prop.Value + Environment.NewLine);
                        }*/
                        textBox1.AppendText(getTs() + "Found USB HDD: " + disk["Name"] + " (" + disk["VolumeName"] + ")" + Environment.NewLine);
                        comboBox1.Items.Add(disk["Name"] + " (" + disk["VolumeName"] + ")");
                        usbDrives.Add((string)disk["Name"]);
                        i++;
                    }
                }
            }
            if (i < 1)
            {
                MessageBox.Show("Unable to detect a USB hard drive. Please plug in the device and run this software again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            else
            {
                comboBox1.SelectedIndex = 0;
            }
            CheckForIllegalCrossThreadCalls = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool bHandled = false;
            switch (keyData)
            {
                case Keys.F5:
                    if (Height == debugSize)
                    {
                        Height = normalSize;
                    }
                    else
                    {
                        Height = debugSize;
                    }
                    break;
            }
            return bHandled;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Ready to start backup?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                extDrive = usbDrives[comboBox1.SelectedIndex];
                textBox1.AppendText(getTs() + "Backup starting..." + Environment.NewLine);
                textBox1.AppendText(getTs() + "Target: " + extDrive + extDir + Environment.NewLine);
                label3.Text = "Backing up... this may take some time..";
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.Enabled = true;

                backgroundWorker1.RunWorkerAsync();
            }
        }

        public string getTs()
        {
            DateTime dt = DateTime.Now;
            return "[" + dt.ToString() + "] ";
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = "C:\\windows\\system32\\robocopy.exe";
            if (!Directory.Exists(extDrive + extDir))
            {
                try
                {
                    textBox1.AppendText(getTs() + " Target directory does not exist. Attempting to create." + Environment.NewLine);
                    DirectoryInfo di = Directory.CreateDirectory(logDir);
                }
                catch (Exception ex)
                {
                    logging = false;
                    textBox1.AppendText(getTs() + "Unable to create target directory. Aborting! " + Environment.NewLine);
                    MessageBox.Show("Cannot create target directory! " + Environment.NewLine + Environment.NewLine + extDrive + extDir, "Fatal error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                    Close();
                }
            }

            if (!e.Cancel)
            {
                p.StartInfo.Arguments = sourceDir + " " + extDrive + extDir + " /MIR /FFT /R:0 /W:0 /Z /NP /NDL";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.EnableRaisingEvents = true;
                p.StartInfo.CreateNoWindow = true;

                p.OutputDataReceived += new DataReceivedEventHandler((s, de) => { try { textBox1.AppendText(getTs() + de.Data + Environment.NewLine); label3.Text = de.Data.Replace("\t", " "); } catch (Exception ex) { } });
                p.ErrorDataReceived += new DataReceivedEventHandler((s, de) => { try { textBox1.AppendText(getTs() + de.Data + Environment.NewLine); label3.Text = de.Data.Replace("\t", " "); } catch (Exception ex) { } });
                p.Exited += new EventHandler((s, de) =>
                {
                    DateTime dt = DateTime.Now;
                    string logF = "tptbackup_" + dt.Ticks.ToString() + ".log";
                    File.WriteAllText(logDir + "\\" + logF, textBox1.Text);
                    MessageBox.Show("Backup has been completed!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    label3.Text = "Backup complete!";
                    Close();
                });

                p.Start();
                p.BeginOutputReadLine();
                p.WaitForExit();
            }
        }
    }
}
