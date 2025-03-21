# Unity Personal Package Manager

A lightweight package management system for Unity projects, consisting of a Unity Editor window and a C# console server application. This system allows teams to share and manage custom packages without requiring a full-scale asset server.

## Overview

This system consists of two main components:

1. **Unity Editor Window** - A custom editor window that allows you to browse, download, and upload packages
2. **C# Console Server** - A standalone server application that manages the package repository

The system is designed to be simple to set up and use, making it ideal for small teams or individual developers who want to maintain a library of reusable assets.

## Features

- Browse available packages
- Download packages directly into your Unity project
- Upload new packages or update existing ones
- Search for packages by name or description
- Track upload and download progress
- Support for large file uploads through chunking
- Simple JSON-based package database

## Setup

### Server Setup

1. Clone this repository
2. Open the server solution in Visual Studio
3. Build the console application
4. Run the server application

The server will create a `packages` directory and a `package_db.json` file in its working directory to store packages and metadata.

### Unity Editor Setup

1. Copy the `PackageManagerWindow.cs` file to your Unity project's `Editor` folder
2. Open the Package Manager window in Unity via `Window > Package Manager > Personal Package Manager`
3. Configure the server URL (default is `http://localhost:5005`)

## How It Works

### Package Storage

Packages are stored on the server as ZIP files in the `packages` directory. Each package has its own subdirectory named after the package. The server maintains a JSON database (`package_db.json`) that tracks metadata for each package, including:

- Package name
- Version
- Description
- File path
- Upload date

### Communication Protocol

The Unity Editor and server communicate via HTTP using the following endpoints:

- `GET /api/search?q=query` - Search for packages
- `GET /api/details/packageName` - Get details for a specific package
- `GET /api/download/packageName/version` - Download a package
- `POST /api/upload` - Upload a package using multipart form data
- `POST /api/upload-json` - Upload a package using JSON with base64-encoded file data
- `POST /api/upload-chunk` - Upload a chunk of a large package (for files >2GB)

### Package Download Process

1. The Unity Editor sends a request to download a specific package version
2. The server locates the package in its database
3. The server sends the package ZIP file to the Unity Editor
4. The Unity Editor extracts the ZIP file to the specified location
5. If the extraction location is within the Assets folder, the Asset Database is refreshed

### Package Upload Process

1. The Unity Editor compresses the selected folder into a ZIP file
2. For small files (<2GB), the ZIP file is sent to the server in a single request
3. For large files (>2GB), the ZIP file is split into chunks and sent in multiple requests
4. The server saves the package and updates its database

## Configuration

### Server Configuration

The server listens on port 5005 by default. You can change this by modifying the `PORT` constant in the `Program.cs` file.

### Unity Editor Configuration

The Unity Editor window connects to `http://localhost:5005` by default. You can change this by modifying the `serverUrl` variable in the `PackageManagerWindow.cs` file.

## Limitations

- The server uses HttpListener, which has a 2GB limit for single file uploads (chunked uploads are used for larger files)
- The system does not include user authentication or access control
- Package versioning is manual (you need to specify the version when uploading)

## Extending the System

The system can be extended in several ways:

- Add user authentication
- Implement package dependencies
- Add version control integration
- Create a more sophisticated web interface for the server
- Add package validation and testing

## Troubleshooting

### Common Issues

- **404 Not Found when downloading**: Check that the package name and version match exactly what's in the database
- **Upload errors**: Ensure all required fields are filled in and the package folder exists
- **Package not visible in Unity**: Try refreshing the Asset Database manually (Assets > Refresh)

### Debugging

- The server logs detailed information to the console
- The Unity Editor logs errors and warnings to the Unity Console

## License

This project is licensed under the MIT License - see the LICENSE file for details.
