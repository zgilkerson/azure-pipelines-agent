using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker.TestResults
{
    public class TestCaseResultData
    {
        public int Id;

        public string Comment;

        public ShallowReference Configuration;

        public ShallowReference Project;

        public DateTime StartedDate;

        public DateTime CompletedDate;

        public Double DurationInMs;

        public string Outcome;

        public int Revision;

        public string State;

        public ShallowReference TestCase;

        public ShallowReference TestPoint;

        public ShallowReference TestRun;

        public int ResolutionStateId;

        public string ResolutionState;

        public DateTime LastUpdatedDate;

        public int Priority;

        public string ComputerName;
                
        public int ResetCount;

        public ShallowReference Build;

        public ShallowReference Release;

        public string ErrorMessage;

        public DateTime CreatedDate;

        public List<TestIterationDetailsModel> IterationDetails;

        public List<ShallowReference> AssociatedBugs;

        public string Url;

        public string FailureType;

        public string AutomatedTestName { get; set; }

        public string AutomatedTestStorage;

        public string AutomatedTestType;

        public string AutomatedTestTypeId;

        public string AutomatedTestId;

        public ShallowReference Area;

        public string TestCaseTitle;

        public string StackTrace;

        public List<CustomTestField> CustomFields;

        public BuildReference BuildReference;

        public ReleaseReference ReleaseReference;

        public ShallowReference TestPlan;

        public ShallowReference TestSuite;

        public int TestCaseReferenceId;

        public IdentityRef Owner;

        public IdentityRef RunBy;

        public IdentityRef LastUpdatedBy;

        public ResultGroupType ResultGroupType;

        public List<TestCaseSubResultData> TestCaseSubResultData { get; set; }

        public AttachmentData AttachmentData { get; set; }
    }

    public class TestCaseSubResultData
    {
        public int Id;

        public TestCaseResultIdentifier TestResult { get; set; }

        public int ParentId;

        public int SequenceId;

        public string DisplayName;

        public ResultGroupType ResultGroupType;

        public string Outcome;

        public string Comment;

        public string ErrorMessage;

        public DateTime StartedDate;

        public DateTime CompletedDate;

        public long DurationInMs;

        public ShallowReference Configuration;

        public DateTime LastUpdatedDate;

        public string ComputerName;

        public string StackTrace;

        public List<CustomTestField> CustomFields;

        public string Url;

        public List<TestCaseSubResultData> SubResultData { get; set; }

        public AttachmentData AttachmentData { get; set; }
    }

    public class AttachmentData
    {
        /// <summary>
        /// List of Filepaths of attachments associated with the TestRun.
        /// </summary>
        public IList<string> AttachmentsFilePathList { get; set; }

        /// <summary>
        /// Console log of the Test Run.
        /// </summary>
        public string ConsoleLog { get; set; }

        /// <summary>
        /// Standard Error of the Test Run.
        /// </summary>
        public string StandardError { get; set; }
    }
}