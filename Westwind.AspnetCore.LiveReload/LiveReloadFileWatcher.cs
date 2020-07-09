﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Westwind.AspNetCore.LiveReload
{
    public class LiveReloadFileWatcher
    {

        private static System.IO.FileSystemWatcher FileWatcher;
        private static System.IO.FileSystemWatcher FolderWatcher;
        private static string FolderToMonitorPath;
        private static string FolderToMonitorName;
        private static bool _isFolderCreated = false;
        private static ThrottlingTimer _throttler = new ThrottlingTimer();

        public static void StartFileWatcher()
        {
            if (!LiveReloadConfiguration.Current.LiveReloadEnabled)
                return;

            var path = LiveReloadConfiguration.Current.FolderToMonitor;
            FolderToMonitorPath = Path.GetFullPath(path);
            FolderToMonitorName = Path.GetFileName(FolderToMonitorPath);
            StartFilesWatcher();
            StartFolderWatcher();
        }

        public void StopFileWatcher()
        {
            DisposeFilesWatcher();
            DisposeFolderWatcher();
            _throttler = null;
        }

        private static void StartFilesWatcher()
        {
            FileWatcher = new FileSystemWatcher(FolderToMonitorPath);
            FileWatcher.Filter = "*.*";
            FileWatcher.EnableRaisingEvents = true;
            FileWatcher.IncludeSubdirectories = true;

            FileWatcher.NotifyFilter = NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;

            FileWatcher.Changed += FileWatcher_Changed;
            FileWatcher.Created += FileWatcher_Changed;
            FileWatcher.Renamed += FileWatcher_Renamed;
        }

        private static void StartFolderWatcher()
        {
            var parentPath = Path.GetDirectoryName(FolderToMonitorPath);
            var folderName = Path.GetFileName(FolderToMonitorPath);
            FolderWatcher = new FileSystemWatcher(parentPath);
            FolderWatcher.Filter = folderName;
            FolderWatcher.EnableRaisingEvents = true;
            FolderWatcher.IncludeSubdirectories = false;


            FolderWatcher.Created += FolderWatcher_Created;
            FolderWatcher.Deleted += FolderWatcher_Deleted;
            FolderWatcher.Renamed += FolderWatcher_Renamed;
        }

        private static void DisposeFilesWatcher()
        {
            FileWatcher.Changed -= FileWatcher_Changed;
            FileWatcher.Created -= FileWatcher_Changed;
            FileWatcher.Renamed -= FileWatcher_Renamed;
            FileWatcher.EnableRaisingEvents = false;
            FileWatcher?.Dispose();
            FileWatcher = null;
        }

        private static void DisposeFolderWatcher()
        {
            FolderWatcher.Created -= FolderWatcher_Created;
            FolderWatcher.Deleted -= FolderWatcher_Deleted;
            FolderWatcher.Renamed -= FolderWatcher_Renamed;
            FolderWatcher.EnableRaisingEvents = false;
            FolderWatcher?.Dispose();
            FolderWatcher = null;
        }

        private static List<string> _extensionList;
        private static void FileChanged(string filename)
        {
            if (filename.Contains("\\node_modules\\"))
                return;

            if (string.IsNullOrEmpty(filename) ||
                !LiveReloadConfiguration.Current.LiveReloadEnabled)
                return;

            var ext = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext))
                return;

            if (LiveReloadConfiguration.Current.FileIncludeFilter is Func<string, bool> filter &&
                !filter.Invoke(filename))
                return;

            if (_extensionList == null)
                _extensionList = LiveReloadConfiguration.Current.ClientFileExtensions
                    .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

            if (_extensionList.Contains(ext,StringComparer.OrdinalIgnoreCase))
            {
                if (_isFolderCreated)
                {

                    _throttler.Debounce(2000, param =>
                    {
                        _ = LiveReloadMiddleware.RefreshWebSocketRequest();
                        _isFolderCreated = false;
                    });
                }
                else
                {
                    // delayed - no longer needed as server restarts automatically refresh on restart
                    //bool delayed = ext == ".cshtml" || ext == ".cs" || ext == ".json"  || ext == ".xml";
                    _ = LiveReloadMiddleware.RefreshWebSocketRequest(); // delayed
                }
            }

        }

        private static void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            FileChanged(e.FullPath);
        }

        private static void FileWatcher_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            FileChanged(e.FullPath);
        }

        private static void FolderWatcher_Created(object sender, FileSystemEventArgs e)
        {
            _isFolderCreated = true;
            StartFilesWatcher();
        }

        private static void FolderWatcher_Deleted(object sender, System.IO.FileSystemEventArgs e)
        {
            DisposeFilesWatcher();
        }

        private static void FolderWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (string.Compare(e.Name, FolderToMonitorName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                StartFileWatcher();
            }
            else if (string.Compare(e.OldName, FolderToMonitorName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                DisposeFilesWatcher();
            }
        }
    }
}






