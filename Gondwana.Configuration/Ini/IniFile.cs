using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Gondwana.Scripting.Ini
{
    public class IniFile : IDisposable
    {
        #region Win32 p/invoke
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
        public static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName,
            string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WritePrivateProfileString(string lpAppName, string lpKeyName,
            string lpString, string lpFileName);
        #endregion

        #region event declaration
        public delegate void DirtyOnCloseEventHandler(DirtyCloseEventArgs dcea);
        public event DirtyOnCloseEventHandler DirtyOnClose;
        #endregion

        #region private fields
        private readonly string iniFilePath;
        private bool saveIfDirty = false;
        private bool dirty = false;
        private Dictionary<string, Dictionary<string, string>> values = 
            new Dictionary<string, Dictionary<string, string>>();
        #endregion

        #region constructor / finalizer
        public IniFile(string path)
        {
            iniFilePath = path;
        }

        ~IniFile()
        {
            Dispose();
        }
        #endregion

        #region public properties
        public string FilePath
        {
            get { return iniFilePath; }
        }

        public bool SaveIfDirty
        {
            get { return saveIfDirty; }
            set { saveIfDirty = value; }
        }

        public bool Dirty
        {
            get { return dirty; }
        }
        #endregion

        #region public methods
        public void SaveToFile()
        {
            // for each section...
            foreach (KeyValuePair<string, Dictionary<string, string>> section in values)
            {
                // for each key / value within each section...
                foreach (KeyValuePair<string, string> val in section.Value)
                {
                    WritePrivateProfileString(section.Key, val.Key, val.Value, iniFilePath);
                }
            }

            dirty = false;
        }

        public string GetValue(string section, string key)
        {
            if (_SectionKeyLoaded(section, key))
                return values[section][key];
            else
            {
                _SetSectionIfNotExists(section);

                StringBuilder retBuilder = new StringBuilder(512);
                string retString;

                if (GetPrivateProfileString(section, key, string.Empty, retBuilder, 512, iniFilePath) == 0)
                    retString = string.Empty;
                else
                    retString = retBuilder.ToString();

                values[section].Add(key, retString);
                return retString;
            }
        }

        public void SetValue(string section, string key, string val)
        {
            if (_SectionKeyLoaded(section, key))
                values[section][key] = val;
            else
            {
                _SetSectionIfNotExists(section);
                values[section].Add(key, val);
            }

            dirty = true;
        }
        #endregion

        #region private methods
        private bool _SectionKeyLoaded(string section, string key)
        {
            if (values.ContainsKey(section) == false)
                return false;
            else
                return values[section].ContainsKey(key);
        }

        private void _SetSectionIfNotExists(string section)
        {
            if (values.ContainsKey(section) == false)
                values.Add(section, new Dictionary<string, string>());
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (dirty)
            {
                if (saveIfDirty)
                    SaveToFile();
                else
                {
                    if (DirtyOnClose != null)
                    {
                        DirtyCloseEventArgs dcea = new DirtyCloseEventArgs(this);
                        DirtyOnClose(dcea);

                        if (dcea.SaveValues)
                            SaveToFile();
                    }
                }
            }
        }
        #endregion
    }
}
