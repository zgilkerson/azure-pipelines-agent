using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Services.Agent
{
    [ServiceLocator(Default = typeof(PagingLogger))]
    public interface IPagingLogger : IAgentService
    {
        void Setup(Guid timelineId, Guid timelineRecordId, bool performCourtesyDebugLogging, Guid debugTimelineId, Guid debugTimelineRecordId);

        void Write(string message, bool isDebugLogMessage);

        void End();
    }

    public class PagingLogger : AgentService, IPagingLogger
    {
        public static string PagingFolder = "pages";
        private static string DebugPrefix = "debug";

        // 8 MB
        public const int PageSize = 8 * 1024 * 1024;

        private Guid _timelineId;
        private Guid _timelineRecordId;

        // Used for courtesy debug logging
        private bool _performCourtesyDebugLogging;
        private Guid _debugTimelineId;
        private Guid _debugTimelineRecordId;
        
        private string _pagesFolder;
        private IJobServerQueue _jobServerQueue;

        // Standard Logging
        private string _pageId;
        private int _byteCount;
        private int _pageCount;
        private FileStream _pageData;
        private StreamWriter _pageWriter;
        private string _dataFileName;

        // Courtesy Debug Logging
        private int _debugByteCount;
        private int _debugPageCount;
        private FileStream _debugPageData;
        private StreamWriter _debugPageWriter;
        private string _debugDataFileName;

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            _pageId = Guid.NewGuid().ToString();
            _pagesFolder = Path.Combine(hostContext.GetDirectory(WellKnownDirectory.Diag), PagingFolder);
            _jobServerQueue = HostContext.GetService<IJobServerQueue>();
            Directory.CreateDirectory(_pagesFolder);
        }

        public void Setup(Guid timelineId, Guid timelineRecordId, bool performCourtesyDebugLogging, Guid debugTimelineId, Guid debugTimelineRecordId)
        {
            _timelineId = timelineId;
            _timelineRecordId = timelineRecordId;

            _performCourtesyDebugLogging = performCourtesyDebugLogging;
            _debugTimelineId = debugTimelineId;
            _debugTimelineRecordId = debugTimelineRecordId;
        }

        //
        // Write a metadata file with id etc, point to pages on disk.
        // Each page is a guid_#.  As a page rolls over, it events it's done
        // and the consumer queues it for upload
        // Ensure this is lazy.  Create a page on first write
        //
        public void Write(string message, bool isDebugLogMessage)
        {
            string line = $"{DateTime.UtcNow.ToString("O")} {message}";

            // _performCourtesyDebugLogging: true, isDebugLogMessage: true
                // debug log: yes, normal log: no
            // _performCourtesyDebugLogging: true, isDebugLogMessage: false
                // debug log: yes, normal log: yes

            // _performCourtesyDebugLogging: false, isDebugLogMessage: true
                // debug log: no, normal log: yes (if they set system.debug to true)
            // _performCourtesyDebugLogging: false, isDebugLogMessage: false
                // debug log: no, normal log: yes

            if ((_performCourtesyDebugLogging && !isDebugLogMessage) || 
                (!_performCourtesyDebugLogging))
            {
                WriteLine(line);
            }

            // If we are performing debug logging then we should log any message, regardless of 
            // whether or not it's a debug message.
            if (_performCourtesyDebugLogging)
            {
                WriteDebugLine(line);
            }
        }

        private void WriteLine(string line)
        {
            // lazy creation on write
            if (_pageWriter == null)
            {
                Create();
            }

            _pageWriter.WriteLine(line);
            _byteCount += System.Text.Encoding.UTF8.GetByteCount(line);
            if (_byteCount >= PageSize)
            {
                NewPage();
            }
        }

        private void WriteDebugLine(string line)
        {
            if (_performCourtesyDebugLogging)
            {
                // TODO: Maybe change the order?
                if (_debugPageWriter == null)
                {
                    CreateDebug();
                }

                _debugPageWriter.WriteLine(line);
                _debugByteCount += System.Text.Encoding.UTF8.GetByteCount(line);
                if (_debugByteCount >= PageSize)
                {
                    NewDebugPage();
                }
            }
        }

        public void End()
        {
            EndPage();

            if (_performCourtesyDebugLogging)
            {
                EndDebugPage();
            }
        }

        // TODO: Make sure that this happens before we empty the job server queue
        // public void FlushDebugLog(Guid timelineId, Guid timelineRecordId)
        // {
        //     if (_performCourtesyDebugLogging && 
        //         _debugFileNames != null && 
        //         _debugFileNames.Any())
        //     {
        //         foreach (string debugFileName in _debugFileNames)
        //         {
        //             _jobServerQueue.QueueFileUpload(timelineId, timelineRecordId, "DistributedTask.Core.Log", "CustomToolLog", debugFileName, true);
        //         }
        //     }
        // }

        private void Create()
        {
            NewPage();
        }

        private void CreateDebug()
        {
            NewDebugPage();
        }

        private void NewPage()
        {
            EndPage();
            _byteCount = 0;
            _dataFileName = Path.Combine(_pagesFolder, $"{_pageId}_{++_pageCount}.log");
            _pageData = new FileStream(_dataFileName, FileMode.CreateNew);
            _pageWriter = new StreamWriter(_pageData, System.Text.Encoding.UTF8);
        }

        private void NewDebugPage()
        {
            if (_performCourtesyDebugLogging)
            {
                EndDebugPage();
                _debugByteCount = 0;
                _debugDataFileName = Path.Combine(_pagesFolder, $"{DebugPrefix}_{_pageId}_{++_debugPageCount}.log");
                _debugPageData = new FileStream(_debugDataFileName, FileMode.CreateNew);
                _debugPageWriter = new StreamWriter(_debugPageData, System.Text.Encoding.UTF8);
            }
        }

        private void EndPage()
        {
            if (_pageWriter != null)
            {
                _pageWriter.Flush();
                _pageData.Flush();
                _pageWriter.Dispose(); //The StreamWriter object calls Dispose() on the provided Stream object when StreamWriter.Dispose is called.
                _pageWriter = null;
                _pageData = null;
                _jobServerQueue.QueueFileUpload(_timelineId, _timelineRecordId, "DistributedTask.Core.Log", "CustomToolLog", _dataFileName, true);
            }
        }

        private void EndDebugPage()
        {
            if (_performCourtesyDebugLogging && 
                _debugPageWriter != null)
            {
                _debugPageWriter.Flush();
                _debugPageData.Flush();
                _debugPageWriter.Dispose();
                _debugPageWriter = null;
                _debugPageData = null;
                _jobServerQueue.QueueDebugFileUpload(_debugTimelineId, _debugTimelineRecordId, "DistributedTask.Core.Log", "CustomToolLog", _debugDataFileName, true);
            }
        }
    }
}