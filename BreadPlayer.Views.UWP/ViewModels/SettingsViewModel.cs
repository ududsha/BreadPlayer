﻿/* 
	BreadPlayer. A music player made for Windows 10 store.
    Copyright (C) 2016  theweavrs (Abdullah Atta)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using BreadPlayer.Common;
using BreadPlayer.Core;
using BreadPlayer.Core.Common;
using BreadPlayer.Core.Extensions;
using BreadPlayer.Core.Models;
using BreadPlayer.Database;
using BreadPlayer.Dialogs;
using BreadPlayer.Extensions;
using BreadPlayer.Helpers;
using BreadPlayer.Messengers;
using BreadPlayer.PlaylistBus;
using BreadPlayer.Themes;

namespace BreadPlayer.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        #region Properties

        private bool _enableBlur;
        public bool EnableBlur
        {
            get => _enableBlur;
            set
            {
                Set(ref _enableBlur, value);
                RoamingSettingsHelper.SaveSetting("EnableBlur", value);
            }
        }

        private bool _preventScreenFromLocking;
        public bool PreventScreenFromLocking
        {
            get => _preventScreenFromLocking;
            set
            {
                Set(ref _preventScreenFromLocking, value);
                if (value)
                {
                    KeepScreenActive();
                }
                else
                {
                    ReleaseDisplayRequest();
                }
            }
        }

        private bool _replaceLockscreenWithAlbumArt;
        public bool ReplaceLockscreenWithAlbumArt
        {
            get => _replaceLockscreenWithAlbumArt;
            set
            {
                Set(ref _replaceLockscreenWithAlbumArt, value);
                RoamingSettingsHelper.SaveSetting("ReplaceLockscreenWithAlbumArt", value);
            }
        }

        private string _uiTextType;
        public string UiTextType
        {
            get => _uiTextType;
            set
            {
                Set(ref _uiTextType, value);
                RoamingSettingsHelper.SaveSetting("UITextType", _uiTextType);
            }
        }

        private bool _isThemeDark;
        public bool IsThemeDark
        {
            get => _isThemeDark;
            set
            {
                Set(ref _isThemeDark, value);
                RoamingSettingsHelper.SaveSetting("SelectedTheme", _isThemeDark ? "Light" : "Dark");
                // SharedLogic.InitializeTheme();
            }
        }

        public ThreadSafeObservableCollection<StorageFolder> _LibraryFoldersCollection;
        public ThreadSafeObservableCollection<StorageFolder> LibraryFoldersCollection
        {
            get
            {
                if (_LibraryFoldersCollection == null)
                {
                    _LibraryFoldersCollection = new ThreadSafeObservableCollection<StorageFolder>();
                }
                return _LibraryFoldersCollection;
            }
            set => Set(ref _LibraryFoldersCollection, value);
        }
        public static GroupedObservableCollection<string, Mediafile> TracksCollection
        { get; set; }

        private string _timeClosed;
        public string TimeClosed
        {
            get => _timeClosed;
            set => Set(ref _timeClosed, value);
        }

        private string _timeOpened;
        public string TimeOpened
        {
            get => _timeOpened;
            set => Set(ref _timeOpened, value);
        }

        private List<StorageFile> _modifiedFiles = new List<StorageFile>();
        public List<StorageFile> ModifiedFiles
        {
            get => _modifiedFiles;
            set => Set(ref _modifiedFiles, value);
        }

        private bool _changeAccentByAlbumart;
        public bool ChangeAccentByAlbumArt
        {
            get => _changeAccentByAlbumart;
            set
            {
                Set(ref _changeAccentByAlbumart, value);
                if (value == false)
                {
                    ThemeManager.SetThemeColor(null);
                }
                else
                {
                    ThemeManager.SetThemeColor("default");
                }

                RoamingSettingsHelper.SaveSetting("ChangeAccentByAlbumArt", _changeAccentByAlbumart);
            }
        }

        private LibraryService LibraryService { get; set; }
        #endregion

        #region MessageHandling
        private async void HandleLibraryLoadedMessage(Message message)
        {
            if (message.Payload is List<object> list)
            {
                TracksCollection = list[0] as GroupedObservableCollection<string, Mediafile>;
                if (LibraryService.SongCount <= 0)
                {
                    await AutoLoadMusicLibraryAsync().ConfigureAwait(false);
                }
                if (TracksCollection != null)
                {
                    message.HandledStatus = MessageHandledStatus.HandledContinue;
                }
            }
        }
        #endregion

        #region Ctor  
        public SettingsViewModel()
        {
            LibraryService = new LibraryService(new KeyValueStoreDatabaseService(SharedLogic.DatabasePath, "Tracks", "TracksText"));
            PropertyChanged += SettingsViewModel_PropertyChanged;
            _changeAccentByAlbumart = RoamingSettingsHelper.GetSetting<bool>("ChangeAccentByAlbumArt", true);
            _timeOpened = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _uiTextType = RoamingSettingsHelper.GetSetting<string>("UITextType", "Normal");
            _isThemeDark = RoamingSettingsHelper.GetSetting<string>("SelectedTheme", "Light") == "Light" ? true : false;
            _enableBlur = RoamingSettingsHelper.GetSetting<bool>("EnableBlur", !InitializeCore.IsMobile);
            _replaceLockscreenWithAlbumArt = RoamingSettingsHelper.GetSetting<bool>("replaceLockscreenWithAlbumArt", false);
            Messenger.Instance.Register(MessageTypes.MsgLibraryLoaded, new Action<Message>(HandleLibraryLoadedMessage));
        }
        #endregion

        #region Commands

        #region Definitions   

        private RelayCommand _navigateCommand;
        /// <summary>
        /// Gets load library command. This calls the <see cref="Load"/> method.
        /// </summary>
        public RelayCommand NavigateCommand { get { if (_navigateCommand == null) { _navigateCommand = new RelayCommand(Navigate); } return _navigateCommand; } }

        private DelegateCommand _loadCommand;
        /// <summary>
        /// Gets load library command. This calls the <see cref="Load"/> method.
        /// </summary>
        public DelegateCommand LoadCommand { get { if (_loadCommand == null) { _loadCommand = new DelegateCommand(Load); } return _loadCommand; } }

        private DelegateCommand _importPlaylistCommand;
        /// <summary>
        /// Gets load library command. This calls the <see cref="Load"/> method.
        /// </summary>
        public DelegateCommand ImportPlaylistCommand { get { if (_importPlaylistCommand == null) { _importPlaylistCommand = new DelegateCommand(ImportPlaylists); } return _importPlaylistCommand; } }

        private DelegateCommand _resetCommand;
        /// <summary>
        /// Gets load library command. This calls the <see cref="Load"/> method.
        /// </summary>
        public DelegateCommand ResetCommand { get { if (_resetCommand == null) { _resetCommand = new DelegateCommand(Reset); } return _resetCommand; } }

        #endregion

        #region Implementation
        private async void Navigate(object para)
        {
            if (para.ToString() == "bug-report")
            {
                para = "mailto:support@breadplayer.com?subject=Bread%20Player%202.3.0%20Bug%20Report&body=Summary%3A%0A%0A%20%20%5BA%20brief%20sentence%20describing%20the%20issue%5D%0A%0ASteps%20to%20Reproduce%3A%0A%0A%20%201.%20%5BFirst%20Step%5D%0A%20%202.%20%5BSecond%20Step%5D%0A%20%203.%20%5Band%20so%20on...%5D%0A%0AExpected%20behavior%3A%20%5BWhat%20you%20expect%20to%20happen%5D%0A%0AActual%20behavior%3A%20%5BWhat%20actually%20happens%5D%0A%0A%5BAttach%20any%20logs%20situated%20in%3A%20Music%5CBreadPlayerLogs%5C%5D%0A%0A";
            }
            else if (para.ToString() == "feature-request")
            {
                para = "mailto:support@breadplayer.com?subject=Bread%20Player%20Feature%20Request&body=Summary%3A%0A%0A%5BA%20few%20sentences%20describing%20what%20the%20feature%20actually%20is%5D%0A%0AHow%20will%20it%20be%20useful%3A%0A%0A%5BAn%20explanation%20on%20how%20it%20will%20help%5D%0A%0AHelpful%20links%3A%0A%0A%5BIf%20there%20are%20any%20links%20we%20can%20refer%20to%20that%20might%20help%20us%20in%20implementing%20this%20faster%20and%20better%5D%0A%0AAdditional%20Comments%3A%0A%0A%5BIf%20you%20have%20something%20other%20to%20say%20%3A)%5D%0A%0A";
            }

            await Launcher.LaunchUriAsync(new Uri(para.ToString()));
        }
        private async void Reset()
        {
            try
            {
                Messenger.Instance.NotifyColleagues(MessageTypes.MsgDispose);
                LibraryFoldersCollection.Clear();
                await ApplicationData.Current.ClearAsync();
                ResetCommand.IsEnabled = false;
                await Task.Delay(200);
                ResetCommand.IsEnabled = true;
            }
            catch (Exception ex)
            {
                BLogger.Logger.Error("Error occured while resetting the player.", ex);
            }
        }
        private async void ImportPlaylists()
        {
            var picker = new FileOpenPicker();
            FileOpenPicker openPicker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.MusicLibrary
            };
            openPicker.FileTypeFilter.Add(".m3u");
            openPicker.FileTypeFilter.Add(".pls");
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                IPlaylist playlist = null;
                if (Path.GetExtension(file.Path) == ".m3u")
                {
                    playlist = new M3U();
                }
                else
                {
                    playlist = new Pls();
                }

                var plist = new Playlist { Name = file.DisplayName };
                Messenger.Instance.NotifyColleagues(MessageTypes.MsgAddPlaylist, plist);
                await playlist.LoadPlaylist(file).ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Loads songs from a specified folder into the library. <seealso cref="LoadCommand"/>
        /// </summary>
        public async void Load()
        {
            try
            {  //LibVM.Database.RemoveFolder(LibraryFoldersCollection[0].Path);
                FolderPicker picker = new FolderPicker();
                picker.FileTypeFilter.Add(".mp3");
                picker.FileTypeFilter.Add(".wav");
                picker.FileTypeFilter.Add(".ogg");
                picker.FileTypeFilter.Add(".flac");
                picker.FileTypeFilter.Add(".m4a");
                picker.FileTypeFilter.Add(".aif");
                picker.FileTypeFilter.Add(".wma");
                picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                picker.ViewMode = PickerViewMode.List;
                picker.CommitButtonText = "Import";
                StorageFolder folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    await LoadFolderAsync(folder);
                }
            }
            catch(UnauthorizedAccessException)
            {
                await NotificationManager.ShowMessageAsync("You are not authorized to access this folder. Please choose another folder or try again.");
            }
        }

        #endregion

        #endregion

        #region Methods

        #region General Settings Methods

        private DisplayRequest _displayRequest;
        private void KeepScreenActive()
        {
            if (_displayRequest == null)
            {
                _displayRequest = new DisplayRequest();
                // This call activates a display-required request. If successful,  
                // the screen is guaranteed not to turn off automatically due to user inactivity. 
                _displayRequest.RequestActive();
            }
        }
        private void ReleaseDisplayRequest()
        {
            // This call de-activates the display-required request. If successful, the screen 
            // might be turned off automatically due to a user inactivity, depending on the 
            // power policy settings of the system. The requestRelease method throws an exception  
            // if it is called before a successful requestActive call on this object. 
            if (_displayRequest != null)
            {
                _displayRequest.RequestRelease();
                _displayRequest = null;
            }
        }

        #endregion

        #region LoadFoldersCommand
        public async Task LoadFolders()
        {
            if (LibraryFoldersCollection.Count <= 0)
            {
                var folderPaths = RoamingSettingsHelper.GetSetting<string>("folders", null);
                if (folderPaths != null)
                {
                    foreach (var folder in folderPaths.Split('|'))
                    {
                        if (!string.IsNullOrEmpty(folder))
                        {
                            var storageFolder = await StorageFolder.GetFolderFromPathAsync(folder);
                            LibraryFoldersCollection.Add(storageFolder);
                        }
                    }
                }
            }
        }
        #endregion
       
        #region Add Methods
        /// <summary>
        /// Adds modified files got from querying in the <see cref="LibraryFoldersCollection"/>. The query parameters include time range from <see cref="TimeClosed"/> to <seealso cref="TimeOpened"/>.
        /// </summary>
        private async Task AddModifiedFilesAsync()
        {
            TimeClosed = RoamingSettingsHelper.GetSetting<string>("timeclosed", "0");
            ModifiedFiles = await DirectoryWalker.GetModifiedFiles(LibraryFoldersCollection, TimeClosed);
            if (ModifiedFiles.Any())
            {
                RenameAddOrDeleteFiles(ModifiedFiles);
            }
        }
        /// <summary>
        /// Adds storage files into library.
        /// </summary>
        /// <param name="files">List containing StorageFile(s).</param>
        /// <returns></returns>
        public async static Task AddStorageFilesToLibraryAsync(IEnumerable<StorageFile> files)
        {
            foreach (var file in files)
            {
                Mediafile mp3File = null;
                int index = -1;
                if (file != null)
                {
                    if (TracksCollection.Elements.Any(t => t.Path == file.Path))
                    {
                        index = TracksCollection.Elements.IndexOf(TracksCollection.Elements.First(t => t.Path == file.Path));
                        SharedLogic.RemoveMediafile(TracksCollection.Elements.First(t => t.Path == file.Path));
                    }
                    //this methods notifies the Player that one song is loaded. We use both 'count' and 'i' variable here to report current progress.
                    await SharedLogic.NotificationManager.ShowMessageAsync(" Song(s) Loaded");
                    await Task.Run(async () =>
                    {
                        //here we load into 'mp3file' variable our processed Song. This is a long process, loading all the properties and the album art.
                        mp3File = await SharedLogic.CreateMediafile(file, false); //the core of the whole method.
                        await SaveSingleFileAlbumArtAsync(mp3File, file).ConfigureAwait(false);
                    });
                    SharedLogic.AddMediafile(mp3File, index);
                }
            }
        }
        #endregion

        #region Load Methods
        private async Task LoadFolderAsync(StorageFolder folder)
        {
            Messenger.Instance.NotifyColleagues(MessageTypes.MsgUpdateSongCount, (short)2);
            LibraryFoldersCollection.Add(folder);
            StorageApplicationPermissions.FutureAccessList.Add(folder);
            //Get query options with which we search for files in the specified folder
            var options = DirectoryWalker.GetQueryOptions();
            //this is the query result which we recieve after querying in the folder
            StorageFileQueryResult queryResult = folder.CreateFileQueryWithOptions(options);
            //the event for files changed
            queryResult.ContentsChanged += QueryResult_ContentsChanged;
            await AddFolderToLibraryAsync(queryResult);
        }
        /// <summary>
        /// Auto loads the User's Music Libary on first load.
        /// </summary>
        private async Task AutoLoadMusicLibraryAsync()
        {
            try
            {
                await LoadFolderAsync(KnownFolders.MusicLibrary);
            }
            catch (Exception ex)
            {
                BLogger.Logger.Error("Auto Loading of library failed.", ex);
                await NotificationManager.ShowMessageAsync(ex.Message);
            }
        }
        /// <summary>
        /// Add folder to Library asynchronously.
        /// </summary>
        /// <param name="queryResult">The query result after querying in a specific folder.</param>
        /// <returns></returns>
        public async Task AddFolderToLibraryAsync(StorageFileQueryResult queryResult)
        {
            if (queryResult != null)
            {
                //so that no new event is raised. We want to focus on loading.
                _isLibraryLoading = true;

                //this is a temporary list to collect all the processed Mediafiles. We use List because it is fast. Faster than using ObservableCollection directly because of the events firing on every add.
                var tempList = new List<Mediafile>();

                //'count' is for total files got after querying.
                var count = await queryResult.GetItemCountAsync().AsTask().ConfigureAwait(false);
                if (count == 0)
                {
                    string error = "No songs found!";
                    BLogger.Logger.Error("No songs were found!");
                    await NotificationManager.ShowMessageAsync(error);
                    return;
                }

                int failedCount = 0;

                short i = 0;
                await SharedLogic.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        foreach (StorageFile file in await queryResult.GetFilesAsync())
                        {
                            try
                            {
                                Mediafile mp3File = null;
                                i++;
                                Messenger.Instance.NotifyColleagues(MessageTypes.MsgUpdateSongCount, i);
                                mp3File = await SharedLogic.CreateMediafile(file, false).ConfigureAwait(false); //the core of the whole method.
                                mp3File.FolderPath = Path.GetDirectoryName(file.Path);
                                await SaveSingleFileAlbumArtAsync(mp3File, file).ConfigureAwait(false);

                                await NotificationManager.ShowMessageAsync(i + "\\" + count + " Song(s) Loaded", 0);

                                tempList.Add(mp3File);
                                mp3File = null;
                            }
                            catch (Exception ex)
                            {
                                BLogger.Logger.Error("Loading of a song in folder failed.", ex);
                                //we catch and report any exception without distrubing the 'foreach flow'.
                                await NotificationManager.ShowMessageAsync(ex.Message + " || Occured on: " + file.Path);
                                failedCount++;
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        string message1 = ex.Message + "||" + ex.InnerException;
                        await NotificationManager.ShowMessageAsync(message1);
                    }
                    await NotificationManager.ShowMessageAsync("Saving songs into database. Please wait...");

                    await LibraryService.AddMediafiles(tempList);
                    await TracksCollection.AddRange(tempList).ConfigureAwait(false);
                    //await DeleteDuplicates(tempList).ConfigureAwait(false);

                    AlbumArtistViewModel vm = new AlbumArtistViewModel();
                    Messenger.Instance.NotifyColleagues(MessageTypes.MsgUpdateSongCount, "Done!");
                    Messenger.Instance.NotifyColleagues(MessageTypes.MsgAddAlbums, TracksCollection.Elements);
                    vm = null;

                    _isLibraryLoading = false;
                    string message = string.Format("Songs successfully imported! Total Songs: {0}; Failed: {1}; Loaded: {2}", count, failedCount, i);

                    BLogger.Logger.Info(message);
                    await NotificationManager.ShowMessageAsync(message);
                    tempList.Clear();
                });
            }
        }

        private async Task DeleteDuplicates(IEnumerable<Mediafile> source)
        {
            await SharedLogic.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var duplicateFiles = source.Where(s => source.Count(x => x.OrginalFilename == s.OrginalFilename) > 1);
                if (duplicateFiles.Count() > 0)
                {
                    await ShowMessageBox(async selectedDuplicates =>
                    {
                        if (selectedDuplicates.Any())
                        {
                            try
                            {
                                foreach (var duplicate in selectedDuplicates)
                                {
                                    var duplicateIndex = 0;
                                    var duplicateCount = TracksCollection.Elements.Count(t => t.OrginalFilename == duplicate.OrginalFilename);
                                    if (duplicateCount > 2)
                                    {
                                        for (int i = 0; i < duplicateCount - 1; i++)
                                        {
                                            duplicateIndex = TracksCollection.Elements.IndexOf(TracksCollection.Elements.FirstOrDefault(t => t.OrginalFilename == duplicate.OrginalFilename));
                                            if (duplicateIndex > -1)
                                            {
                                                await SharedLogic.RemoveMediafile(TracksCollection.Elements.ElementAt(duplicateIndex)).ConfigureAwait(false);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        await SharedLogic.RemoveMediafile(duplicate).ConfigureAwait(false);
                                    }
                                }
                            }
                            catch(Exception ex)
                            {
                                BLogger.Logger.Error("Error while deleting duplicates.", ex);
                            }
                        }
                    }, duplicateFiles).ConfigureAwait(false);
                }
            });
        }

        private async Task ShowMessageBox(Action<IEnumerable<Mediafile>> action, IEnumerable<Mediafile> duplicates)
        {
            try
            {
                DuplicatesDialog dialog = new DuplicatesDialog()
                {
                    Duplicates = duplicates.DistinctBy(t => t.OrginalFilename),
                    Title = string.Format("Please choose the duplicates you would like to delete. Total duplicates: {0} duplicate songs?", duplicates.Count())
                };
                if (CoreWindow.GetForCurrentThread().Bounds.Width <= 501)
                {
                    dialog.DialogWidth = CoreWindow.GetForCurrentThread().Bounds.Width - 50;
                }
                else
                {
                    dialog.DialogWidth = CoreWindow.GetForCurrentThread().Bounds.Width - 100;
                }

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    action?.Invoke(dialog.SelectedDuplicates);
                }
            }
            catch (UnauthorizedAccessException) { }
        }
        #endregion

        #region AlbumArt Methods
        public static async Task SaveSingleFileAlbumArtAsync(Mediafile mp3File, StorageFile file = null)
        {
            if (mp3File != null)
            {
                try
                {
                    if (file == null)
                    {
                        file = await StorageFile.GetFileFromPathAsync(mp3File.Path);
                    }

                    var albumartFolder = ApplicationData.Current.LocalFolder;
                    var albumartLocation = albumartFolder.Path + @"\AlbumArts\" + (mp3File.Album + mp3File.LeadArtist).ToLower().ToSha1() + ".jpg";

                    if (!SharedLogic.VerifyFileExists(albumartLocation, 300))
                    {
                        bool albumSaved = await SharedLogic.SaveImagesAsync(file, mp3File);                       
                        mp3File.AttachedPicture = albumSaved ? albumartLocation : null;
                    }
                    file = null;
                }
                catch (Exception ex)
                {
                    BLogger.Logger.Info("Failed to save albumart.", ex);
                    await SharedLogic.NotificationManager.ShowMessageAsync("Failed to save album art of " + mp3File.OrginalFilename);
                }
            }
        }

        private static async Task SaveMultipleAlbumArtsAsync(IEnumerable<Mediafile> files)
        {
            foreach(var file in files)
            {
                await SaveSingleFileAlbumArtAsync(file);
            }
        }
        #endregion

        #region Folder Watecher Methods
        /// <summary>
        /// This is still experimental. As you can see, there are more lines of code then necessary.
        /// </summary>
        /// <param name="files"></param>
        public static async void RenameAddOrDeleteFiles(IEnumerable<StorageFile> files)
        {
            try
            {
                string folder = "";
                foreach (var file in files)
                {
                    var props = await file.Properties.GetMusicPropertiesAsync();
                    folder = Path.GetDirectoryName(file.Path);
                    //FOR RENAMING (we check all the tracks to see if we have a track that is the same as the changed file except for its path)
                    if (TracksCollection.Elements.ToArray().Any(t => t.Path != file.Path && t.Title == props.Title && t.LeadArtist == props.Artist && t.Album == props.Album && t.Length == props.Duration.ToString(@"mm\:ss")))
                    {
                        var mp3File = TracksCollection.Elements.FirstOrDefault(t => t.Path != file.Path && t.Title == props.Title && t.LeadArtist == props.Artist && t.Album == props.Album && t.Length == props.Duration.ToString(@"mm\:ss"));
                        mp3File.Path = file.Path;
                    }
                    //FOR ADDITION (we check all the files to see if we already have the file or not)
                    if (TracksCollection.Elements.ToArray().All(t => t.Path != file.Path))
                    {
                        var mediafile = await SharedLogic.CreateMediafile(file, false);
                        SharedLogic.AddMediafile(mediafile);
                        await SaveSingleFileAlbumArtAsync(mediafile);
                    }
                }
                //FOR DELETE (we only want to iterate through songs which are in the changed folder)
                var deletedFiles = new List<Mediafile>();
                foreach (var file in TracksCollection.Elements.ToArray().Where(t => t.FolderPath == folder))
                {
                    if (!File.Exists(file.Path))
                    {
                        deletedFiles.Add(TracksCollection.Elements.FirstOrDefault(t => t.Path == file.Path));
                    }
                }
                if (deletedFiles.Any())
                {
                    foreach (var deletedFile in deletedFiles)
                    {
                        TracksCollection.Elements.Remove(deletedFile);
                    }
                }
            }
            catch (Exception ex)
            {
                BLogger.Logger.Error("Some error occured while renaming, deleting or editting the modified files.", ex);
                await SharedLogic.NotificationManager.ShowMessageAsync(ex.Message);
            }
        }

        public async static Task PerformWatcherWorkAsync(StorageFolder folder)
        {
            StorageFileQueryResult modifiedqueryResult = folder.CreateFileQueryWithOptions(DirectoryWalker.GetQueryOptions("datemodified:>" + SharedLogic.SettingsVm.TimeOpened));
            var files = await modifiedqueryResult.GetFilesAsync();
            if (await modifiedqueryResult.GetItemCountAsync() > 0)
            {
                await AddStorageFilesToLibraryAsync(await modifiedqueryResult.GetFilesAsync());
            }
            //since there were no modifed files returned yet the event was raised, this means that some file was renamed or deleted. To acknowledge that change we need to reload everything in the modified folder
            else
            {
                //this is the query result which we recieve after querying in the folder
                StorageFileQueryResult queryResult = folder.CreateFileQueryWithOptions(DirectoryWalker.GetQueryOptions());
                files = await queryResult.GetFilesAsync();
                RenameAddOrDeleteFiles(files);
            }
        }



        #endregion

        #endregion

        #region Events
        private async void SettingsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ReplaceLockscreenWithAlbumArt")
            {
                if (ReplaceLockscreenWithAlbumArt)
                {
                    _replaceLockscreenWithAlbumArt = await LockscreenHelper.SaveCurrentLockscreenImage();
                }
                else
                {
                    await LockscreenHelper.ResetLockscreenImage();
                }
            }
        }

        private bool _isLibraryLoading;
        private async void QueryResult_ContentsChanged(IStorageQueryResultBase sender, object args)
        {
            if (!_isLibraryLoading)
            {
                await PerformWatcherWorkAsync(sender.Folder).ConfigureAwait(false);
            }
        }
        #endregion

    }
}
