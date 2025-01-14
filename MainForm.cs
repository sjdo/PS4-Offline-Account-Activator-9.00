﻿using libdebug;
using MetroFramework.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MetroFramework.Forms;


namespace PS4OfflineAccountActivator
{
    public partial class MainForm : MetroFramework.Forms.MetroForm
    {
        public PS4DBG ps4 = null;
        public ulong executable = 0;
        public ulong sceRegMgrGetInt_addr = 0;
        public ulong sceRegMgrGetStr_addr = 0;
        public ulong sceRegMgrGetBin_addr = 0;

        public ulong sceRegMgrSetInt_addr = 0;
        public ulong sceRegMgrSetStr_addr = 0;
        public ulong sceRegMgrSetBin_addr = 0;

        public Process p = null;
        public ulong stub = 0;

        public Registry r = new Registry();

        public MainForm()
        {
            InitializeComponent();
        }


        public ulong GetIntNative(uint regId, out int intVal)
        {
            ulong errorCode = 0;

            var bufferAddr = ps4.AllocateMemory(p.pid, sizeof(int));
            ps4.WriteMemory<int>(p.pid, bufferAddr, 0);

            errorCode = ps4.Call(p.pid, stub, sceRegMgrGetInt_addr, regId, bufferAddr);
            int valueReturned = ps4.ReadMemory<int>(p.pid, bufferAddr);

            ps4.FreeMemory(p.pid, bufferAddr, sizeof(int));

            intVal = valueReturned;

            return errorCode;
        }

        public ulong SetIntNative(uint regId, int intVal)
        {
            return ps4.Call(p.pid, stub, sceRegMgrSetInt_addr, regId, intVal);
        }

        public ulong GetStrNative(uint regId, out string strVal, uint strSize)
        {
            ulong errorCode = 0;

            var blankArray = new byte[strSize];

            var bufferAddr = ps4.AllocateMemory(p.pid, (int)strSize);
            ps4.WriteMemory(p.pid, bufferAddr, blankArray);

            errorCode = ps4.Call(p.pid, stub, sceRegMgrGetStr_addr, regId, bufferAddr, strSize);
            var valueReturned = ps4.ReadMemory(p.pid, bufferAddr, (int)strSize);

            ps4.FreeMemory(p.pid, bufferAddr, (int)strSize);

            int len = Array.IndexOf(valueReturned, (byte)0);
            strVal = Encoding.UTF8.GetString(valueReturned, 0, len);


            return errorCode;
        }

        public byte[] Combine(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }

        public ulong SetStrNative(uint regId, string strVal, uint strSize)
        {
            ulong errorCode = 0;

            byte[] temp_bytes = Encoding.ASCII.GetBytes(strVal);
            byte[] bytes = Combine(temp_bytes, new byte[] { 0x0 });

            var bufferAddr = ps4.AllocateMemory(p.pid, bytes.Length);
            ps4.WriteMemory(p.pid, bufferAddr, bytes);

            errorCode = ps4.Call(p.pid, stub, sceRegMgrSetStr_addr, regId, bufferAddr, bytes.Length);

            ps4.FreeMemory(p.pid, bufferAddr, bytes.Length);
            return errorCode;
        }

        public ulong GetBinNative(uint regId, out byte[] binVal, uint binSize)
        {
            ulong errorCode = 0;

            var blankArray = new byte[binSize];

            var bufferAddr = ps4.AllocateMemory(p.pid, (int)binSize);
            ps4.WriteMemory(p.pid, bufferAddr, blankArray);
            errorCode = ps4.Call(p.pid, stub, sceRegMgrGetBin_addr, regId, bufferAddr, binSize);
            binVal = ps4.ReadMemory(p.pid, bufferAddr, (int)binSize);
            ps4.FreeMemory(p.pid, bufferAddr, (int)binSize);

            return errorCode;
        }

        public ulong SetBinNative(uint regId, byte[] binVal, uint binSize)
        {
            ulong errorCode = 0;
            var bufferAddr = ps4.AllocateMemory(p.pid, (int)binSize);
            ps4.WriteMemory(p.pid, bufferAddr, binVal);
            errorCode = ps4.Call(p.pid, stub, sceRegMgrSetBin_addr, regId, bufferAddr, binSize);
            ps4.FreeMemory(p.pid, bufferAddr, (int)binSize);
            return errorCode;
        }

        public byte[] GetAccountId(int userNumber)
        {
            ulong errorCode = 0;
            byte[] psnAccountId = null;
            errorCode = GetBinNative((uint)r.KEY_account_id(userNumber), out psnAccountId, Registry.SIZE_account_id);
            return psnAccountId;
        }

        public ulong SetAccountId(int userNumber, byte[] psnAccountId)
        {
            ulong errorCode = 0;

            errorCode = SetBinNative((uint)r.KEY_account_id(userNumber), psnAccountId, Registry.SIZE_account_id);
            //errorCode = GetBinNative((uint)r.KEY_account_id(userNumber), out psnAccountId, Registry.SIZE_account_id);

            string text = "np";
            errorCode = SetStrNative((uint)r.KEY_NP_env(userNumber), text, (uint)text.Length);

            errorCode = SetIntNative((uint)r.KEY_login_flag(userNumber), 6);

            return errorCode;

        }

        private void btConnect_Click(object sender, EventArgs e)
        {
            metroTextBox1.Text = metroTextBox1.Text;
            try
            {
                ps4 = new PS4DBG(metroTextBox1.Text);
                ps4.Connect();

                ProcessList pl = ps4.GetProcessList();

                p = pl.FindProcess("SceShellUI");

                ProcessMap pi = ps4.GetProcessMaps(p.pid);
                executable = 0;
                for (int i = 0; i < pi.entries.Length; i++)
                {
                    MemoryEntry me = pi.entries[i];
                    if (me.prot == 5)
                    {
                        Console.WriteLine("executable base " + me.start.ToString("X"));
                        executable = me.start;
                        break;
                    }
                }

                stub = ps4.InstallRPC(p.pid);

                sceRegMgrGetInt_addr = executable + 0x809680;
                sceRegMgrGetStr_addr = executable + 0x808EC0;
                sceRegMgrGetBin_addr = executable + 0x80AA40;

                sceRegMgrSetInt_addr = executable + 0x80B3C0;
                sceRegMgrSetStr_addr = executable + 0x80F5B0;
                sceRegMgrSetBin_addr = executable + 0x80AA50;


                if (ps4.IsConnected)
                {
                    metroTextBox2.Text = "Connected to " + metroTextBox1.Text + ". Click Get Users";
                    btGetUsers.Enabled = true;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString(), "Something went wrong and it's probably your fault ;-P");
            }
        }

        private void btGetUsers_Click(object sender, EventArgs e)
        {
            metroTextBox2.Text = "Getting Users... ";
            string userName = "";
            for (int i = 1; i <= 16; i++)
            {
                userName = GetUserName(i);
                if (userName.Length > 0)
                {
                    (this.Controls.Find("lbUsername" + i.ToString(), true)[0] as Label).Text = userName;
                    (this.Controls.Find("tbPSNId" + i.ToString(), true)[0] as MetroTextBox).Text = GetAccountIdText(i);
                    (this.Controls.Find("metroButton" + i.ToString(), true)[0] as Button).Enabled = true;
                }
            }

            metroTextBox2.Text = "Got Users. Type in the account id and click the Set Id & Activate button of the specific user";

        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }

        private string GetAccountIdText(int userNumber)
        {
            byte[] temp = GetAccountId(userNumber);
            Array.Reverse(temp);
            return ByteArrayToString(temp);
        }

        private string GetUserName(int userNumber)
        {
            string outString = "";
            ulong errorCode = 0;
            errorCode = GetStrNative((uint)r.KEY_user_name(userNumber), out outString, Registry.SIZE_user_name);

            return outString;
        }

        private void SetAccountIdText(int userNumber, string accountId)
        {
            byte[] temp = StringToByteArray(accountId);
            Array.Reverse(temp);
            SetAccountId(userNumber, temp);
        }

        private void metroButton5_Click(object sender, EventArgs e)
        {
            SetAccountIdText(5, tbPSNId5.Text);
            string text = "np";
            SetStrNative((uint)r.KEY_NP_env(1), text, (uint)text.Length);
            SetIntNative((uint)r.KEY_login_flag(1), 6);
            metroTextBox2.Text = "Account id set && activated. Click Get Users to verify it was written properly";
        }

        private void metroButton4_Click(object sender, EventArgs e)
        {
            SetAccountIdText(4, tbPSNId4.Text);
            string text = "np";
            SetStrNative((uint)r.KEY_NP_env(2), text, (uint)text.Length);
            SetIntNative((uint)r.KEY_login_flag(2), 6);
            metroTextBox2.Text = "Account id set && activated. Click Get Users to verify it was written properly";
        }

        private void metroButton3_Click(object sender, EventArgs e)
        {
            SetAccountIdText(3, tbPSNId3.Text);
            string text = "np";
            SetStrNative((uint)r.KEY_NP_env(3), text, (uint)text.Length);
            SetIntNative((uint)r.KEY_login_flag(3), 6);
            metroTextBox2.Text = "Account id set && activated. Click Get Users to verify it was written properly";
        }

        private void metroButton2_Click(object sender, EventArgs e)
        {
            SetAccountIdText(2, tbPSNId2.Text);
            string text = "np";
            SetStrNative((uint)r.KEY_NP_env(4), text, (uint)text.Length);
            SetIntNative((uint)r.KEY_login_flag(4), 6);
            metroTextBox2.Text = "Account id set && activated. Click Get Users to verify it was written properly";
        }

        private void metroButton1_Click(object sender, EventArgs e)
        {
            SetAccountIdText(1, tbPSNId1.Text);
            string text = "np";
            SetStrNative((uint)r.KEY_NP_env(5), text, (uint)text.Length);
            SetIntNative((uint)r.KEY_login_flag(5), 6);
            metroTextBox2.Text = "Account id set && activated. Click Get Users to verify it was written properly";
        }

    }
}


