// ------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------

using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace OneDriveApiBrowser
{
    public partial class FormBrowser : Form
    {
        public const string MsaClientId = "c35c09da-4ac5-47e7-98cc-2ba2979d8ae7";
        public const string MsaReturnUrl = "http://localhost";


        private enum ClientType
        {
            Consumer,
            Business
        }

        private GraphServiceClient graphClient { get; set; }
        private ClientType clientType { get; set; }
        private DriveItem CurrentFolder { get; set; }
        private DriveItem SelectedItem { get; set; }

        private OneDriveTile _selectedTile;

        public FormBrowser()
        {
            InitializeComponent();
        }

        private void ShowWork(bool working)
        {
            BeginInvoke(new Action(() =>
            {
                this.UseWaitCursor = working;
                this.progressBar1.Visible = working;
            }));
        }

        private async Task LoadFolderFromId(string id)
        {
            if (graphClient is null)
            {
                return;
            }

            // Update the UI for loading something new
            ShowWork(true);
            LoadChildren(Enumerable.Empty<DriveItem>());

            try
            {
                var expandString = this.clientType == ClientType.Consumer
                    ? "thumbnails,children($expand=thumbnails)"
                    : "thumbnails,children";

                var folder = await this.graphClient.Drive.Items[id].Request().Expand(expandString).GetAsync().ConfigureAwait(true);
                ProcessFolder(folder);
            }
            catch (Exception exception)
            {
                PresentServiceException(exception);
            }
            ShowWork(false);
        }

        private async Task LoadFolderFromPath(string path = null)
        {
            if (null == this.graphClient) return;

            // Update the UI for loading something new
            ShowWork(true);
            LoadChildren(Enumerable.Empty<DriveItem>());

            try
            {
                DriveItem folder;

                var expandValue = this.clientType == ClientType.Consumer
                    ? "thumbnails,children($expand=thumbnails)"
                    : "thumbnails,children";

                if (path == null)
                {
                    folder = await this.graphClient.Drive.Root.Request().Expand(expandValue).GetAsync();
                }
                else
                {
                    folder =
                        await
                            this.graphClient.Drive.Root.ItemWithPath("/" + path)
                                .Request()
                                .Expand(expandValue)
                                .GetAsync();
                }

                ProcessFolder(folder);
            }
            catch (Exception exception)
            {
                PresentServiceException(exception);
            }

            ShowWork(false);
        }

        private void ProcessFolder(DriveItem folder)
        {
            if (folder != null)
            {
                this.CurrentFolder = folder;

                LoadProperties(folder);

                if (folder.Folder != null && folder.Children != null && folder.Children.CurrentPage != null)
                {
                    LoadChildren(folder.Children.CurrentPage);
                }
            }
        }

        private void LoadProperties(DriveItem item)
        {
            this.SelectedItem = item;

            BeginInvoke(new Action(() =>
            {
                objectBrowser.SelectedItem = item;
            }));
        }

        private void LoadChildren(IEnumerable<DriveItem> items)
        {
            BeginInvoke(new Action(() =>
            {
                flowLayoutContents.SuspendLayout();
                flowLayoutContents.Controls.Clear();

                // Load the children
                foreach (var obj in items)
                {
                    AddItemToFolderContents(obj);
                }

                flowLayoutContents.ResumeLayout();
            }));
        }

        private void AddItemToFolderContents(DriveItem obj)
        {
            flowLayoutContents.Controls.Add(CreateControlForChildObject(obj));
        }

        private void RemoveItemFromFolderContents(DriveItem itemToDelete)
        {
            flowLayoutContents.Controls.RemoveByKey(itemToDelete.Id);
        }

        private Control CreateControlForChildObject(DriveItem item)
        {
            OneDriveTile tile = new OneDriveTile(this.graphClient);
            tile.SourceItem = item;
            tile.Click += ChildObject_Click;
            tile.DoubleClick += ChildObject_DoubleClick;
            tile.Name = item.Id;
            return tile;
        }

        void ChildObject_DoubleClick(object sender, EventArgs e)
        {
            var item = ((OneDriveTile)sender).SourceItem;

            // Look up the object by ID
            NavigateToFolder(item);
        }
        void ChildObject_Click(object sender, EventArgs e)
        {
            if (null != _selectedTile)
            {
                _selectedTile.Selected = false;
            }

            var item = ((OneDriveTile)sender).SourceItem;
            LoadProperties(item);
            _selectedTile = (OneDriveTile)sender;
            _selectedTile.Selected = true;
        }

        private void FormBrowser_Load(object sender, EventArgs e)
        {

        }

        private void NavigateToFolder(DriveItem folder)
        {
            Task t = LoadFolderFromId(folder.Id);

            // Fix up the breadcrumbs
            var breadcrumbs = flowLayoutPanelBreadcrumb.Controls;
            bool existingCrumb = false;
            foreach (LinkLabel crumb in breadcrumbs)
            {
                if (crumb.Tag == folder)
                {
                    RemoveDeeperBreadcrumbs(crumb);
                    existingCrumb = true;
                    break;
                }
            }

            if (!existingCrumb)
            {
                LinkLabel label = new LinkLabel();
                label.Text = "> " + folder.Name;
                label.LinkArea = new LinkArea(2, folder.Name.Length);
                label.LinkClicked += linkLabelBreadcrumb_LinkClicked;
                label.AutoSize = true;
                label.Tag = folder;
                flowLayoutPanelBreadcrumb.Controls.Add(label);
            }
        }

        private async void linkLabelBreadcrumb_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel link = (LinkLabel)sender;

            RemoveDeeperBreadcrumbs(link);

            DriveItem item = link.Tag as DriveItem;
            if (item == null)
            {
                await LoadFolderFromPath(null);
            }
            else
            {
                await LoadFolderFromId(item.Id);
            }
        }

        private void RemoveDeeperBreadcrumbs(LinkLabel link)
        {
            // Remove the breadcrumbs deeper than this item
            var breadcrumbs = flowLayoutPanelBreadcrumb.Controls;
            int indexOfControl = breadcrumbs.IndexOf(link);
            for (int i = breadcrumbs.Count - 1; i > indexOfControl; i--)
            {
                breadcrumbs.RemoveAt(i);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void UpdateConnectedStateUx(bool connected)
        {
            signInMsaToolStripMenuItem.Visible = !connected;
            signOutToolStripMenuItem.Visible = connected;
            flowLayoutPanelBreadcrumb.Visible = connected;
            flowLayoutContents.Visible = connected;
            goToAppFolderToolStripMenuItem.Visible = connected;
            uploadFileToolStripMenuItem.Visible = connected;
            downloadToolStripMenuItem.Visible = connected;
        }

        private async void signInMsaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await this.SignIn();
        }

        private async Task SignIn()
        {

            try
            {
                this.graphClient = AuthenticationHelper.GetAuthenticatedClient();
            }
            catch (ServiceException exception)
            {
                PresentServiceException(exception);
            }

            try
            {
                await LoadFolderFromPath();
                UpdateConnectedStateUx(true);
            }
            catch (ServiceException exception)
            {
                PresentServiceException(exception);
                this.graphClient = null;
            }
        }

        private void signOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.graphClient != null)
            {
                AuthenticationHelper.SignOut();
            }

            UpdateConnectedStateUx(false);
        }

        private System.IO.Stream GetFileStreamForUpload(string targetFolderName, out string originalFilename)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Upload to " + targetFolderName;
            dialog.Filter = "All Files (*.*)|*.*";
            dialog.CheckFileExists = true;
            var response = dialog.ShowDialog();
            if (response != DialogResult.OK)
            {
                originalFilename = null;
                return null;
            }

            try
            {
                originalFilename = System.IO.Path.GetFileName(dialog.FileName);
                return new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Open);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error uploading file: " + ex.Message);
                originalFilename = null;
                return null;
            }
        }

        private async void simpleUploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var targetFolder = this.CurrentFolder;

            string filename;
            using (var stream = GetFileStreamForUpload(targetFolder.Name, out filename))
            {
                if (stream != null)
                {
                    // Since the ItemWithPath method is available only at Drive.Root, we need to strip
                    // /drive/root: (12 characters) from the parent path string.
                    string folderPath = targetFolder.ParentReference?.Path == null
                        ? ""
                        : targetFolder.ParentReference.Path.Remove(0, 12) + "/" + Uri.EscapeUriString(targetFolder.Name);
                    var uploadPath = folderPath + "/" + Uri.EscapeUriString(System.IO.Path.GetFileName(filename));
                    try
                    {
                        var uploadedItem = await this.graphClient.Drive.Root.ItemWithPath(uploadPath).Content.Request().PutAsync<DriveItem>(stream);
                        AddItemToFolderContents(uploadedItem);
                        MessageBox.Show("Uploaded with ID: " + uploadedItem.Id);
                    }
                    catch (Exception exception)
                    {
                        PresentServiceException(exception);
                    }
                }
            }
        }

        private async void simpleIDbasedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var targetFolder = this.CurrentFolder;

            string filename;
            using (var stream = GetFileStreamForUpload(targetFolder.Name, out filename))
            {
                if (stream != null)
                {
                    try
                    {
                        var uploadedItem =
                            await
                                this.graphClient.Drive.Items[targetFolder.Id].ItemWithPath(filename).Content.Request()
                                    .PutAsync<DriveItem>(stream);

                        AddItemToFolderContents(uploadedItem);

                        MessageBox.Show("Uploaded with ID: " + uploadedItem.Id);
                    }
                    catch (Exception exception)
                    {
                        PresentServiceException(exception);
                    }
                }
            }
        }

        private async void createFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormInputDialog dialog = new FormInputDialog("Create Folder", "New folder name:");
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dialog.InputText))
            {
                try
                {
                    var folderToCreate = new DriveItem { Name = dialog.InputText, Folder = new Folder() };
                    var newFolder =
                        await this.graphClient.Drive.Items[this.SelectedItem.Id].Children.Request()
                            .AddAsync(folderToCreate);

                    if (newFolder != null)
                    {
                        MessageBox.Show("Created new folder with ID " + newFolder.Id);
                        this.AddItemToFolderContents(newFolder);
                    }
                }
                catch (ServiceException exception)
                {
                    PresentServiceException(exception);

                }
                catch (Exception exception)
                {
                    PresentServiceException(exception);
                }
            }
        }

        private static void PresentServiceException(Exception exception)
        {
            string message = null;
            var oneDriveException = exception as ServiceException;
            if (oneDriveException == null)
            {
                message = exception.Message;
            }
            else
            {
                message = string.Format("{0}{1}", Environment.NewLine, oneDriveException.ToString());
            }

            MessageBox.Show(string.Format("OneDrive reported the following error: {0}", message));
        }

        private async void deleteSelectedItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var itemToDelete = this.SelectedItem;
            var result = MessageBox.Show("Are you sure you want to delete " + itemToDelete.Name + "?", "Confirm Delete", MessageBoxButtons.YesNo);
            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                try
                {
                    await this.graphClient.Drive.Items[itemToDelete.Id].Request().DeleteAsync();

                    RemoveItemFromFolderContents(itemToDelete);
                    MessageBox.Show("Item was deleted successfully");
                }
                catch (Exception exception)
                {
                    PresentServiceException(exception);
                }
            }
        }

        private async void getChangesHereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var result =
                    await this.graphClient.Drive.Items[this.CurrentFolder.Id].Delta().Request().GetAsync();

                foreach (DriveItem item in result)
                {
                    Console.WriteLine(item.Name);
                }
            }
            catch (Exception ex)
            {
                PresentServiceException(ex);
            }
        }

        private async void saveSelectedFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var item = this.SelectedItem;
            if (null == item)
            {
                MessageBox.Show("Nothing selected.");
                return;
            }

            var dialog = new SaveFileDialog();
            dialog.FileName = item.Name;
            dialog.Filter = "All Files (*.*)|*.*";
            var result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
                return;

            using (var stream = await this.graphClient.Drive.Items[item.Id].Content.Request().GetAsync())
            using (var outputStream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create))
            {
                await stream.CopyToAsync(outputStream);
            }
        }

        private void goToAppFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                DriveItem item = await graphClient.Me.Drive.Special.AppRoot.Request().GetAsync().ConfigureAwait(false);
                if (item == null)
                {
                    await LoadFolderFromPath(null);
                }
                else
                {
                    await LoadFolderFromId(item.Id).ConfigureAwait(false);
                }
            });
        }

        private async void checkOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (SelectedItem is null)
                {
                    MessageBox.Show("No selected file!");
                }

                //await graphClient.Drive.Items[SelectedItem.Id].Checkout().Request().PostAsync();
                await graphClient.Drives[SelectedItem.ParentReference.DriveId].Items[SelectedItem.Id].Checkout().Request().PostAsync();
                // await graphClient.Me.Drive.Items[SelectedItem.Id].Checkout().Request().PostAsync();
            }
            catch (Exception ex)
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    MessageBox.Show("Unexpected error: " + ex.Message);
                });
            }
        }

        private async void checkInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (SelectedItem is null)
                {
                    MessageBox.Show("No selected file!");
                }

                await graphClient.Me.Drive.Items[SelectedItem.Id].Checkin().Request().PostAsync();
            }
            catch (Exception ex)
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    MessageBox.Show("Unexpected error: " + ex.Message);
                });
            }
        }

        private async void uploadAsLargeFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DriveItem targetFolder = this.CurrentFolder;

            string filename;
            using (var stream = GetFileStreamForUpload(targetFolder.Name, out filename))
            {
                if (stream != null)
                {
                    try
                    {
                        Dispatcher.CurrentDispatcher.Invoke(() =>
                        {
                            progressBar1.Visible = true;
                            progressBar1.Style = ProgressBarStyle.Continuous;
                        });
                        
                        // Use properties to specify the conflict behavior
                        // in this case, replace
                        DriveItemUploadableProperties uploadProps = new DriveItemUploadableProperties
                        {
                            ODataType = null,
                            AdditionalData = new Dictionary<string, object>
                            {
                                { "@microsoft.graph.conflictBehavior", "replace" }
                            }
                        };

                        // Create the upload session
                        // itemPath does not need to be a path to an existing item
                        UploadSession uploadSession = await graphClient.Me.Drive.Items[targetFolder.Id]
                            .ItemWithPath(filename)
                            .CreateUploadSession(uploadProps)
                            .Request()
                            .PostAsync();

                        // Max slice size must be a multiple of 320 KiB
                        int maxSliceSize = 320 * 1024;
                        var fileUploadTask =
                            new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxSliceSize);

                        // Create a callback that is invoked after each slice is uploaded
                        long streamLength = stream.Length;
                        IProgress<long> progress = new Progress<long>(prog =>
                        {
                            Dispatcher.CurrentDispatcher.Invoke(() =>
                            {
                                progressBar1.Value = (int)(prog / streamLength * 100);
                            });
                        });

                        try
                        {
                            // Upload the file
                            UploadResult<DriveItem> uploadResult = await fileUploadTask.UploadAsync(progress);

                            if (uploadResult.UploadSucceeded)
                            {
                                AddItemToFolderContents(uploadResult.ItemResponse);
                                // The ItemResponse object in the result represents the
                                // created item.
                                MessageBox.Show($"Upload complete, item ID: {uploadResult.ItemResponse.Id}");
                            }
                            else
                            {
                                MessageBox.Show($"Upload failed");
                            }
                        }
                        catch (ServiceException ex)
                        {
                            PresentServiceException(ex);
                        }

                    }
                    catch (Exception exception)
                    {
                        PresentServiceException(exception);
                    }
                    finally
                    {
                        Dispatcher.CurrentDispatcher.Invoke(() =>
                        {
                            progressBar1.Visible = false;
                            progressBar1.Style = ProgressBarStyle.Marquee;
                        });
                    }
                }
            }
        }
    }
}
