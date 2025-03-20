using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.IO.Compression;
using System.Text;

public class PackageManagerWindow : EditorWindow
{
    private string searchQuery = "";
    private Vector2 scrollPosition;
    private List<PackageInfo> packages = new List<PackageInfo>();
    private string packageName = "";
    private string packageVersion = "";
    private string packageDescription = "";
    private string packageFolderPath = "";
    private bool isUploading = false;
    private bool isDownloading = false;
    private string statusMessage = "";
    private string serverUrl = "http://localhost:5005";
    private GUIStyle headerStyle;
    private GUIStyle packageStyle;
    private GUIStyle buttonStyle;
    private enum Tab { Browse, Upload, Settings }
    private Tab currentTab = Tab.Browse;

    // Progress tracking variables
    private float uploadProgress = 0f;
    private float downloadProgress = 0f;
    private bool showUploadProgress = false;
    private bool showDownloadProgress = false;
    private UnityWebRequest activeUploadRequest;
    private UnityWebRequest activeDownloadRequest;

    [MenuItem("Window/Pesonal Package Manager")]
    public static void ShowWindow()
    {
        GetWindow<PackageManagerWindow>("Package Manager");
    }

    private void OnEnable()
    {
        LoadPackages();
    }

    private void CreateStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 16;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.margin = new RectOffset(5, 5, 10, 10);
        }

        if (packageStyle == null)
        {
            packageStyle = new GUIStyle(EditorStyles.helpBox);
            packageStyle.margin = new RectOffset(5, 5, 5, 5);
            packageStyle.padding = new RectOffset(10, 10, 10, 10);
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.margin = new RectOffset(5, 5, 5, 5);
        }
    }

    private void OnGUI()
    {
        CreateStyles();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(currentTab == Tab.Browse, "Browse", EditorStyles.toolbarButton))
            currentTab = Tab.Browse;
        if (GUILayout.Toggle(currentTab == Tab.Upload, "Upload", EditorStyles.toolbarButton))
            currentTab = Tab.Upload;
        if (GUILayout.Toggle(currentTab == Tab.Settings, "Settings", EditorStyles.toolbarButton))
            currentTab = Tab.Settings;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        switch (currentTab)
        {
            case Tab.Browse:
                DrawBrowseTab();
                break;
            case Tab.Upload:
                DrawUploadTab();
                break;
            case Tab.Settings:
                DrawSettingsTab();
                break;
        }

        // Display progress bars if operations are in progress
        if (showDownloadProgress)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Download Progress:");
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(progressRect, downloadProgress, $"{(downloadProgress * 100):F0}%");

            if (GUILayout.Button("Cancel Download"))
            {
                CancelDownload();
            }
        }

        if (showUploadProgress)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upload Progress:");
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(progressRect, uploadProgress, $"{(uploadProgress * 100):F0}%");

            if (GUILayout.Button("Cancel Upload"))
            {
                CancelUpload();
            }
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            if (GUILayout.Button("Clear"))
                statusMessage = "";
        }
    }

    private void DrawBrowseTab()
    {
        EditorGUILayout.LabelField("Browse Packages", headerStyle);

        EditorGUILayout.BeginHorizontal();
        searchQuery = EditorGUILayout.TextField("Search", searchQuery);
        if (GUILayout.Button("Search", GUILayout.Width(100)))
        {
            SearchPackages(searchQuery);
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(100)))
        {
            LoadPackages();
        }
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (packages.Count == 0)
        {
            EditorGUILayout.LabelField("No packages found.");
        }
        else
        {
            foreach (var package in packages)
            {
                EditorGUILayout.BeginVertical(packageStyle);
                EditorGUILayout.LabelField($"{package.name} ({package.version})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(package.description);
                EditorGUILayout.LabelField($"Uploaded: {package.uploadDate}");

                EditorGUILayout.BeginHorizontal();
                GUI.enabled = !isDownloading;
                if (GUILayout.Button("Download", buttonStyle))
                {
                    DownloadPackage(package.name, package.version);
                }
                if (GUILayout.Button("Details", buttonStyle))
                {
                    GetPackageDetails(package.name);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawUploadTab()
    {
        EditorGUILayout.LabelField("Upload New Package", headerStyle);

        EditorGUILayout.LabelField("Select a folder to package and upload");

        // Add a clear label indicating description is required
        EditorGUILayout.LabelField("Package Description (required):", EditorStyles.boldLabel);
        packageDescription = EditorGUILayout.TextArea(packageDescription, GUILayout.Height(100));

        // Show warning if description is empty
        if (string.IsNullOrEmpty(packageDescription))
        {
            EditorGUILayout.HelpBox("A description is required to enable upload", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Package Folder", GUILayout.Width(120));
        EditorGUILayout.LabelField(string.IsNullOrEmpty(packageFolderPath) ? "No folder selected" : packageFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(100)))
        {
            packageFolderPath = EditorUtility.OpenFolderPanel("Select Folder to Package", "", "");
            if (!string.IsNullOrEmpty(packageFolderPath))
            {
                // Set package name to folder name
                packageName = Path.GetFileName(packageFolderPath);

                // Set version to current date in YYYY-MM-DD format
                packageVersion = DateTime.Now.ToString("yyyy-MM-dd");

                // Force repaint to update UI
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();

        // Display the auto-generated package name and version
        EditorGUILayout.LabelField($"Package Name: {packageName}");
        EditorGUILayout.LabelField($"Version: {packageVersion}");

        // Make requirements more explicit with visual feedback
        bool canUpload = !isUploading &&
                        !string.IsNullOrEmpty(packageFolderPath) &&
                        !string.IsNullOrEmpty(packageDescription);

        GUI.enabled = canUpload;

        if (GUILayout.Button("Upload Package"))
        {
            CreateAndUploadPackage();
        }

        if (!canUpload && !string.IsNullOrEmpty(packageFolderPath) && string.IsNullOrEmpty(packageDescription))
        {
            EditorGUILayout.HelpBox("Please add a description to enable the upload button", MessageType.Warning);
        }

        GUI.enabled = true;
    }

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Settings", headerStyle);
        serverUrl = EditorGUILayout.TextField("Server URL", serverUrl);

        if (GUILayout.Button("Test Connection"))
        {
            TestConnection();
        }
    }

    private void LoadPackages()
    {
        try
        {
            SearchPackages("");
        }
        catch (Exception ex)
        {
            statusMessage = $"Error loading packages: {ex.Message}";
            Debug.LogError($"Error loading packages: {ex}");
        }
    }

    private void SearchPackages(string query)
    {
        statusMessage = "Searching packages...";
        packages.Clear();

        // URL encode the query to handle special characters
        string encodedQuery = UnityWebRequest.EscapeURL(query);

        UnityWebRequest www = UnityWebRequest.Get($"{serverUrl}/api/search?q={encodedQuery}");
        www.SendWebRequest();

        EditorApplication.update += () => {
            if (www.isDone)
            {
                EditorApplication.update -= EditorApplication.update.GetInvocationList()[EditorApplication.update.GetInvocationList().Length - 1] as EditorApplication.CallbackFunction;

                if (www.result != UnityWebRequest.Result.Success)
                {
                    statusMessage = $"Error: {www.error}";
                    return;
                }

                string json = www.downloadHandler.text;
                PackageInfo[] results = JsonUtility.FromJson<PackageInfoArray>("{\"items\":" + json + "}").items;
                if (results != null)
                {
                    packages.AddRange(results);
                }
                statusMessage = $"Found {packages.Count} packages";
                Repaint();

                www.Dispose();
            }
        };
    }

    private void GetPackageDetails(string packageName)
    {
        statusMessage = $"Getting details for {packageName}...";

        string encodedName = UnityWebRequest.EscapeURL(packageName);
        UnityWebRequest www = UnityWebRequest.Get($"{serverUrl}/api/details/{encodedName}");

        EditorApplication.update += () => {
            if (www.isDone)
            {
                EditorApplication.update -= EditorApplication.update.GetInvocationList()[EditorApplication.update.GetInvocationList().Length - 1] as EditorApplication.CallbackFunction;

                if (www.result != UnityWebRequest.Result.Success)
                {
                    statusMessage = $"Error: {www.error}";
                    return;
                }

                string json = www.downloadHandler.text;
                statusMessage = $"Package details: {json}";
                Repaint();

                www.Dispose();
            }
        };
    }

    private void DownloadPackage(string packageName, string version)
    {
        string downloadPath = EditorUtility.SaveFolderPanel("Select Download Location", "", "");
        if (string.IsNullOrEmpty(downloadPath))
        {
            statusMessage = "Download cancelled";
            return;
        }

        // Check if the download path is within the Assets folder
        bool isWithinAssetsFolder = downloadPath.Replace('\\', '/').Contains(Application.dataPath.Replace('\\', '/'));

        isDownloading = true;
        showDownloadProgress = true;
        downloadProgress = 0f;
        statusMessage = $"Downloading {packageName} v{version}...";

        string encodedName = UnityWebRequest.EscapeURL(packageName);
        string encodedVersion = UnityWebRequest.EscapeURL(version);
        activeDownloadRequest = UnityWebRequest.Get($"{serverUrl}/api/download/{encodedName}/{encodedVersion}");

        // Set up download progress tracking
        UnityWebRequestAsyncOperation operation = activeDownloadRequest.SendWebRequest();

        EditorApplication.update += () => {
            if (activeDownloadRequest == null)
            {
                EditorApplication.update -= EditorApplication.update.GetInvocationList()[EditorApplication.update.GetInvocationList().Length - 1] as EditorApplication.CallbackFunction;
                return;
            }

            // Update progress
            if (!activeDownloadRequest.isDone)
            {
                downloadProgress = operation.progress;
                Repaint(); // Force UI update to show progress
                return;
            }

            EditorApplication.update -= EditorApplication.update.GetInvocationList()[EditorApplication.update.GetInvocationList().Length - 1] as EditorApplication.CallbackFunction;

            if (activeDownloadRequest.result != UnityWebRequest.Result.Success)
            {
                statusMessage = $"Error: {activeDownloadRequest.error}";
                isDownloading = false;
                showDownloadProgress = false;
                activeDownloadRequest = null;
                Repaint();
                return;
            }

            // Create a temporary zip file
            string tempZipPath = Path.Combine(downloadPath, $"{packageName}-{version}.zip");
            File.WriteAllBytes(tempZipPath, activeDownloadRequest.downloadHandler.data);

            // Extract the package to the folder with just the package name (no date)
            string extractPath = Path.Combine(downloadPath, packageName);

            try
            {
                // Check if the folder already exists
                if (!Directory.Exists(extractPath))
                {
                    // Use System.IO.Compression for extraction
                    ZipFile.ExtractToDirectory(tempZipPath, extractPath);
                    statusMessage = $"Package downloaded and extracted to {extractPath}";

                    // Refresh the AssetDatabase if the extraction was within the Assets folder
                    if (isWithinAssetsFolder)
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        statusMessage += " (Assets refreshed)";
                    }
                    else
                    {
                        statusMessage += " (Outside project, no refresh needed)";
                    }
                }
                else
                {
                    statusMessage = $"Folder {extractPath} already exists. Please choose a different location or delete the existing folder.";
                }

                // Delete the temporary zip file
                File.Delete(tempZipPath);
            }
            catch (Exception ex)
            {
                statusMessage = $"Downloaded but extraction failed: {ex.Message}";
                Debug.LogError($"Extraction error: {ex}");
            }

            isDownloading = false;
            showDownloadProgress = false;
            activeDownloadRequest = null;
            Repaint();
        };
    }

    private void CancelDownload()
    {
        if (activeDownloadRequest != null)
        {
            activeDownloadRequest.Abort();
            activeDownloadRequest.Dispose();
            activeDownloadRequest = null;
            isDownloading = false;
            showDownloadProgress = false;
            statusMessage = "Download cancelled";
            Repaint();
        }
    }

    private void CreateAndUploadPackage()
    {
        if (string.IsNullOrEmpty(packageFolderPath) || !Directory.Exists(packageFolderPath))
        {
            statusMessage = "Invalid folder path. Please select a valid folder.";
            return;
        }

        isUploading = true;
        showUploadProgress = true;
        uploadProgress = 0f;
        statusMessage = "Creating package...";

        try
        {
            // Create a temporary zip file with the folder name and date
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            string zipFilePath = Path.Combine(tempDir, $"{packageName}_{packageVersion}.zip");

            Debug.Log($"Creating zip file at: {zipFilePath}");

            // Update status to show we're creating the zip
            statusMessage = "Creating zip file...";
            Repaint();

            // Create the zip file from the selected folder
            ZipFile.CreateFromDirectory(packageFolderPath, zipFilePath);

            Debug.Log($"Zip file created successfully. Size: {new FileInfo(zipFilePath).Length} bytes");

            // Update status to show we're starting the upload
            statusMessage = "Starting upload...";
            Repaint();

            // Upload the zip file
            UploadPackageFile(zipFilePath);
        }
        catch (Exception ex)
        {
            statusMessage = $"Error creating package: {ex.Message}";
            Debug.LogError($"Error creating package: {ex}");
            isUploading = false;
            showUploadProgress = false;
            Repaint();
        }
    }

    private void UploadPackageFile(string zipFilePath)
    {
        Debug.Log($"Uploading package file: {zipFilePath}");

        isUploading = true;
        showUploadProgress = true;
        uploadProgress = 0f;
        statusMessage = "Preparing package for upload...";

        // Read the file as bytes
        byte[] fileData = File.ReadAllBytes(zipFilePath);

        // Create a simple JSON payload with base64-encoded file data
        var uploadData = new Dictionary<string, string>
    {
        { "name", packageName },
        { "version", packageVersion },
        { "description", packageDescription },
        { "fileName", Path.GetFileName(zipFilePath) },
        { "fileData", Convert.ToBase64String(fileData) }
    };

        // Convert to JSON
        string jsonPayload = JsonUtility.ToJson(new UploadRequest
        {
            name = packageName,
            version = packageVersion,
            description = packageDescription,
            fileName = Path.GetFileName(zipFilePath),
            fileData = Convert.ToBase64String(fileData)
        });

        // Log debug info
        Debug.Log($"Package name: {packageName}");
        Debug.Log($"Package version: {packageVersion}");
        Debug.Log($"Package description: {packageDescription}");
        Debug.Log($"File size: {fileData.Length} bytes");

        // Create the web request
        UnityWebRequest www = new UnityWebRequest($"{serverUrl}/api/upload-json", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        // Send the request
        activeUploadRequest = www;
        UnityWebRequestAsyncOperation operation = activeUploadRequest.SendWebRequest();

        EditorApplication.update += () => {
            if (activeUploadRequest == null)
            {
                EditorApplication.update -= EditorApplication.update.GetInvocationList()[EditorApplication.update.GetInvocationList().Length - 1] as EditorApplication.CallbackFunction;
                return;
            }

            // Update progress
            if (!activeUploadRequest.isDone)
            {
                uploadProgress = operation.progress;
                statusMessage = $"Uploading package... {(uploadProgress * 100):F0}%";
                Repaint(); // Force UI update to show progress
                return;
            }

            EditorApplication.update -= EditorApplication.update.GetInvocationList()[EditorApplication.update.GetInvocationList().Length - 1] as EditorApplication.CallbackFunction;

            // Clean up the temporary zip file
            try
            {
                File.Delete(zipFilePath);
                Directory.Delete(Path.GetDirectoryName(zipFilePath));
                Debug.Log("Temporary files cleaned up successfully");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to clean up temporary files: {ex.Message}");
            }

            if (activeUploadRequest.result != UnityWebRequest.Result.Success)
            {
                statusMessage = $"Error: {activeUploadRequest.error}";
                Debug.LogError($"Upload error: {activeUploadRequest.error}");
                Debug.LogError($"Response: {activeUploadRequest.downloadHandler.text}");
                isUploading = false;
                showUploadProgress = false;
                activeUploadRequest = null;
                Repaint();
                return;
            }

            string json = activeUploadRequest.downloadHandler.text;
            Debug.Log($"Upload response: {json}");

            UploadResponse response = JsonUtility.FromJson<UploadResponse>(json);

            if (response.success)
            {
                statusMessage = "Package uploaded successfully!";
                packageName = "";
                packageVersion = "";
                packageDescription = "";
                packageFolderPath = "";
                LoadPackages();
            }
            else
            {
                statusMessage = $"Error: {response.message}";
            }

            isUploading = false;
            showUploadProgress = false;
            activeUploadRequest = null;
            Repaint();
        };
    }
    [Serializable]
    private class UploadRequest
    {
        public string name;
        public string version;
        public string description;
        public string fileName;
        public string fileData;
    }

    private void CancelUpload()
    {
        if (activeUploadRequest != null)
        {
            activeUploadRequest.Abort();
            activeUploadRequest.Dispose();
            activeUploadRequest = null;
            isUploading = false;
            showUploadProgress = false;
            statusMessage = "Upload cancelled";
            Repaint();
        }
    }

    private void TestConnection()
    {
        statusMessage = "Testing connection...";

        UnityWebRequest www = UnityWebRequest.Get($"{serverUrl}/");
        www.SendWebRequest();

        EditorApplication.update += () => {
            if (www.isDone)
            {
                EditorApplication.update -= EditorApplication.update.GetInvocationList()[EditorApplication.update.GetInvocationList().Length - 1] as EditorApplication.CallbackFunction;

                if (www.result != UnityWebRequest.Result.Success)
                {
                    statusMessage = $"Connection failed: {www.error}";
                }
                else
                {
                    statusMessage = "Connection successful!";
                }

                Repaint();

                www.Dispose();
            }
        };
    }

    [Serializable]
    private class PackageInfo
    {
        public string name;
        public string version;
        public string description;
        public string upload_date;

        // Property to handle the different naming in Unity
        public string uploadDate => upload_date;
    }

    [Serializable]
    private class PackageInfoArray
    {
        public PackageInfo[] items;
    }

    [Serializable]
    private class UploadResponse
    {
        public bool success;
        public string message;
    }

    private void UploadPackageInChunks(string zipFilePath)
    {
        Debug.Log($"Uploading package file in chunks: {zipFilePath}");

        // Read the file
        byte[] fileData = File.ReadAllBytes(zipFilePath);

        // Define chunk size (e.g., 1MB)
        const int chunkSize = 1024 * 1024;

        // Calculate total chunks
        int totalChunks = (int)Math.Ceiling((double)fileData.Length / chunkSize);

        Debug.Log($"File size: {fileData.Length} bytes, splitting into {totalChunks} chunks");

        isUploading = true;
        showUploadProgress = true;
        uploadProgress = 0f;
        statusMessage = "Uploading package in chunks...";

        // Upload each chunk
        UploadChunk(zipFilePath, fileData, 0, totalChunks, chunkSize);
    }

    private void UploadChunk(string zipFilePath, byte[] fileData, int chunkIndex, int totalChunks, int chunkSize)
    {
        // Calculate the size of this chunk
        int start = chunkIndex * chunkSize;
        int end = Math.Min(start + chunkSize, fileData.Length);
        int length = end - start;

        // Create a buffer for this chunk
        byte[] chunkData = new byte[length];
        Buffer.BlockCopy(fileData, start, chunkData, 0, length);

        // Create JSON payload
        string jsonPayload = JsonUtility.ToJson(new ChunkUploadRequest
        {
            name = packageName,
            version = packageVersion,
            description = packageDescription,
            chunkIndex = chunkIndex,
            totalChunks = totalChunks,
            chunkData = Convert.ToBase64String(chunkData)
        });

        // Create the web request
        UnityWebRequest www = new UnityWebRequest($"{serverUrl}/api/upload-chunk", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        // Send the request
        UnityWebRequestAsyncOperation operation = www.SendWebRequest();

        operation.completed += (AsyncOperation op) => {
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error uploading chunk {chunkIndex + 1}/{totalChunks}: {www.error}");
                statusMessage = $"Error: {www.error}";
                isUploading = false;
                showUploadProgress = false;
                return;
            }

            // Update progress
            uploadProgress = (float)(chunkIndex + 1) / totalChunks;
            statusMessage = $"Uploading package... {(uploadProgress * 100):F0}%";

            // If this was the last chunk, we're done
            if (chunkIndex == totalChunks - 1)
            {
                Debug.Log("All chunks uploaded successfully!");
                statusMessage = "Package uploaded successfully!";
                packageName = "";
                packageVersion = "";
                packageDescription = "";
                packageFolderPath = "";
                isUploading = false;
                showUploadProgress = false;

                // Clean up the temporary zip file
                try
                {
                    File.Delete(zipFilePath);
                    Directory.Delete(Path.GetDirectoryName(zipFilePath));
                    Debug.Log("Temporary files cleaned up successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to clean up temporary files: {ex.Message}");
                }

                // Refresh the package list
                LoadPackages();
            }
            else
            {
                // Upload the next chunk
                UploadChunk(zipFilePath, fileData, chunkIndex + 1, totalChunks, chunkSize);
            }

            Repaint();
        };
    }

    [Serializable]
    private class ChunkUploadRequest
    {
        public string name;
        public string version;
        public string description;
        public int chunkIndex;
        public int totalChunks;
        public string chunkData;
    }

}