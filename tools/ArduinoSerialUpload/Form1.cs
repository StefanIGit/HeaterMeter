﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace ArduinoSerialUpload
{
    public partial class Form1 : Form
    {
        const int DATAFLASH_PAGE_BYTES = 528;

        string _inBuffer;
        List<FileInfo> _workQueue;
        List<string> _headerTop = new List<string>();
        List<string> _headerBottom = new List<string>();
        int _flashOffset;
        int _flashFileNo; 

        public enum SendState { NONE, WAITFORSEND, SENDINGFILE };
        private SendState _sendState= SendState.NONE;

        public Form1()
        {
            InitializeComponent();
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            _inBuffer = serialPort1.ReadExisting();
            Invoke((Action)(() => {
                serialData(_inBuffer);
            }));
        }

        private void serialData(string s)
        {
            txtLog.AppendText(s);

            string[] sLines = s.Split(new char[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string sLine in sLines)
            {
                bool isOk = sLine.StartsWith("200 OK ");
                if (_sendState == SendState.WAITFORSEND && isOk)
                    _sendState = SendState.SENDINGFILE;
                if (_sendState == SendState.SENDINGFILE && isOk)
                {
                    NextFile();
                    return;
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            serialPort1.Write(txtSendString.Text);
            serialPort1.Write("\n");
            txtSendString.SelectAll();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            serialPort1.DtrEnable = false;
            serialPort1.DtrEnable = true;
        }

        private void btnBrowseOne_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                txtFilename.Text = openFileDialog1.FileName;
        }

        private void btnSendOne_Click(object sender, EventArgs e)
        {
            ResetFileTable();
            SendOneFile(txtFilename.Text);
            DumpFileTable();
        }

        private void DumpFileTable()
        {
            txtLog.AppendText("#ifndef __FLASHFILES_H__" + Environment.NewLine + 
                "#define __FLASHFILES_H__" + Environment.NewLine + Environment.NewLine);
            txtLog.AppendText("#define DATAFLASH_PAGE_BYTES " + DATAFLASH_PAGE_BYTES.ToString() + Environment.NewLine + Environment.NewLine);

            _headerTop.ForEach(s => txtLog.AppendText(s));
            txtLog.AppendText(Environment.NewLine + "const struct flash_file_t {" + Environment.NewLine +
                "  const char *fname;" + Environment.NewLine +
                "  const unsigned int page;" + Environment.NewLine +
                "  const unsigned int size;" + Environment.NewLine +
                "} FLASHFILES[] PROGMEM = {" + Environment.NewLine);
            _headerBottom.ForEach(s => txtLog.AppendText(s));
            txtLog.AppendText("  { 0, 0, 0}," + Environment.NewLine + 
                "};" + Environment.NewLine + Environment.NewLine);

            txtLog.AppendText("#endif /* __FLASHFILES_H__ */" + Environment.NewLine);
        }

        private void ResetFileTable()
        {
            _flashOffset = 1584;
            _flashFileNo = 0;
            _headerTop.Clear();
            _headerBottom.Clear();
        }

        private void SendOneFile(string fileName, string flashName = "")
        {
            if (string.IsNullOrWhiteSpace(flashName))
                flashName = Path.GetFileName(fileName);

            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                _headerTop.Add(string.Format("const char FNAME{0:D3}[] PROGMEM = \"{1}\";" + Environment.NewLine, 
                    _flashFileNo, flashName));
                _headerBottom.Add(string.Format("  {{ FNAME{0:D3}, {1,3:D}, {2:D} }}," + Environment.NewLine, 
                    _flashFileNo, _flashOffset / DATAFLASH_PAGE_BYTES, fs.Length));

                serialPort1.Write("START " + _flashOffset.ToString() + " " + fs.Length.ToString() + "\n");
                _sendState = SendState.WAITFORSEND;

                byte[] myBuff = new byte[DATAFLASH_PAGE_BYTES];
                int r;
                while ((r = fs.Read(myBuff, 0, myBuff.Length)) > 0)
                {
                    serialPort1.Write(myBuff, 0, r);
                    _flashOffset += r;
                }
                fs.Close();
            }

            // Round up to the next page
            _flashOffset = ((_flashOffset / DATAFLASH_PAGE_BYTES) + 1) * DATAFLASH_PAGE_BYTES;
            ++_flashFileNo;
            _sendState = SendState.SENDINGFILE;
        }

        private void btnSendDir_Click(object sender, EventArgs e)
        {
            DirectoryInfo di = new DirectoryInfo(txtDirectory.Text);
            _workQueue = new List<FileInfo>(di.GetFiles());

            ResetFileTable();
            lstWorkQueue.DataSource = _workQueue;
            lstWorkQueue.DisplayMember = "Name";
            NextFile();
        }

        private void NextFile()
        {
            _sendState = SendState.NONE;
            if (_workQueue.Count == 0)
                DumpFileTable();
            else
            {
                SendOneFile(_workQueue[0].FullName);
                _workQueue.RemoveAt(0);
            }
        }

        private void chkComOpen_CheckedChanged(object sender, EventArgs e)
        {
            if (chkComOpen.Checked)
            {
                serialPort1.PortName = "COM" + edtSerialPort.Text;
                serialPort1.Open();
            }
            else
                serialPort1.Close();
        }
    }
}
