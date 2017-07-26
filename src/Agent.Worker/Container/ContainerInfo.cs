using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Container
{
    public class ContainerInfo
    {
        private string _jobImage;
        private List<MountVolume> _mountVolumes;
        Dictionary<string, string> _imageContainerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> _taskImageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string CurrentUserName { get; set; }
        public string CurrentUserId { get; set; }
        public List<MountVolume> MountVolumes
        {
            get
            {
                if (_mountVolumes == null)
                {
                    _mountVolumes = new List<MountVolume>();
                }

                return _mountVolumes;
            }
        }

        public List<String> Images
        {
            get
            {
                return _imageContainerMap.Keys.ToList();
            }
        }

        public List<String> Containers
        {
            get
            {
                return _imageContainerMap.Values.ToList();
            }
        }

        public bool IsContainerExecution
        {
            get
            {
                return _imageContainerMap.Count > 0;
            }
        }

        public void RegisterJobImage(string imageName)
        {
            // Register job level image
            if (!string.IsNullOrEmpty(_jobImage))
            {
                throw new InvalidOperationException(_jobImage);
            }

            if (!string.IsNullOrEmpty(imageName))
            {
                _jobImage = imageName;
                _imageContainerMap[_jobImage] = string.Empty;
            }
        }

        public void RegisterTaskImage(string imageName, string targetTask)
        {
            // Register task level image
            if (!string.IsNullOrEmpty(imageName) && !string.IsNullOrEmpty(targetTask))
            {
                _taskImageMap[targetTask] = imageName;
                if (!_imageContainerMap.ContainsKey(imageName))
                {
                    _imageContainerMap[imageName] = string.Empty;
                }
            }
        }

        public void RegisterContainer(string imageName, string containerId)
        {
            // Register container per image
            if (!string.IsNullOrEmpty(imageName) && !string.IsNullOrEmpty(containerId))
            {
                _imageContainerMap[imageName] = containerId;
            }
        }

        public string GetTargetContainerId(string targetTask)
        {
            if (!string.IsNullOrEmpty(targetTask))
            {
                if (_taskImageMap.ContainsKey(targetTask))
                {
                    string image = _taskImageMap[targetTask];
                    if (_imageContainerMap.ContainsKey(image))
                    {
                        return _imageContainerMap[image];
                    }
                }
            }

            return _imageContainerMap.ContainsKey(_jobImage) ? _imageContainerMap[_jobImage] : string.Empty;
        }
    }

    public class MountVolume
    {
        public MountVolume(string volumePath, bool readOnly = false)
        {
            this.VolumePath = volumePath;
            this.ReadOnly = readOnly;
        }

        public string VolumePath { get; set; }
        public bool ReadOnly { get; set; }
    }

    public class DockerVersion
    {
        public DockerVersion(Version serverVersion, Version clientVersion)
        {
            this.ServerVersion = serverVersion;
            this.ClientVersion = clientVersion;
        }

        public Version ServerVersion { get; set; }
        public Version ClientVersion { get; set; }
    }
}