#load data/data.cake
#load parameters/parameters.cake

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

/// <summary>
/// Object for accessing the tasks provided by this script.
/// </summary>
var IssuesBuildTasks = new IssuesBuildTaskDefinitions();

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup<IssuesData>(setupContext =>
{
    Information("Initializing Cake.Issues.Recipe (Version {0})...", BuildMetaData.Version);
    return new IssuesData(setupContext);
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

IssuesBuildTasks.IssuesTask = Task("Issues")
    .IsDependentOn("Publish-IssuesArtifacts")
    .IsDependentOn("Create-SummaryIssuesReport")
    .IsDependentOn("Report-IssuesToPullRequest")
    .IsDependentOn("Set-PullRequestIssuesState");

IssuesBuildTasks.ReadIssuesTask = Task("Read-Issues")
    .Does<IssuesData>((data) =>
{
    var settings =
        new ReadIssuesSettings(data.RepositoryRootDirectory)
        {
            Format = IssueCommentFormat.Markdown
        };

    // Determine which issue providers should be used.
    var issueProviders = new List<IIssueProvider>();

    if (IssuesParameters.InputFiles.MsBuildXmlFileLoggerLogFilePath != null)
    {
        issueProviders.Add(
            MsBuildIssuesFromFilePath(
                IssuesParameters.InputFiles.MsBuildXmlFileLoggerLogFilePath,
                MsBuildXmlFileLoggerFormat));
    }

    if (IssuesParameters.InputFiles.MsBuildBinaryLogFilePath != null)
    {
        issueProviders.Add(
            MsBuildIssuesFromFilePath(
                IssuesParameters.InputFiles.MsBuildBinaryLogFilePath,
                MsBuildBinaryLogFileFormat));
    }

    if (IssuesParameters.InputFiles.InspectCodeLogFilePath != null)
    {
        issueProviders.Add(
            InspectCodeIssuesFromFilePath(
                IssuesParameters.InputFiles.InspectCodeLogFilePath));
    }

    // Read issues from log files.
    data.AddIssues(
        ReadIssues(
            issueProviders,
            settings));

    Information("{0} issues are found.", data.Issues.Count());
});

IssuesBuildTasks.CreateFullIssuesReportTask = Task("Create-FullIssuesReport")
    .WithCriteria(() => IssuesParameters.Reporting.ShouldCreateFullIssuesReport, "Creating of full issues report is disabled")
    .IsDependentOn("Read-Issues")
    .Does<IssuesData>((data) =>
{
    data.FullIssuesReport = IssuesParameters.OutputDirectory.CombineWithFilePath("report.html");
    EnsureDirectoryExists(IssuesParameters.OutputDirectory);

    // Create HTML report using DevExpress template.
    var settings = 
        GenericIssueReportFormatSettings
            .FromEmbeddedTemplate(GenericIssueReportTemplate.HtmlDxDataGrid)
            .WithOption(HtmlDxDataGridOption.Theme, DevExtremeTheme.MaterialBlueLight);
    CreateIssueReport(
        data.Issues,
        GenericIssueReportFormat(settings),
        data.RepositoryRootDirectory,
        data.FullIssuesReport);
});

IssuesBuildTasks.PublishIssuesArtifactsTask = Task("Publish-IssuesArtifacts")
    .IsDependentOn("Create-FullIssuesReport")
    .Does<IssuesData>((data) =>
{
    if (data.BuildServer == null)
    {
        Information("Not supported build server.");
        return;
    }

    data.BuildServer.PublishIssuesArtifacts(Context, data);
});

IssuesBuildTasks.CreateSummaryIssuesReportTask = Task("Create-SummaryIssuesReport")
    .WithCriteria(() => IssuesParameters.BuildServer.ShouldCreateSummaryIssuesReport, "Creating of summary issues report is disabled")
    .IsDependentOn("Read-Issues")
    .Does<IssuesData>((data) =>
{
    if (data.BuildServer == null)
    {
        Information("Not supported build server.");
        return;
    }

    data.BuildServer.CreateSummaryIssuesReport(Context, data);
});

IssuesBuildTasks.ReportIssuesToPullRequestTask = Task("Report-IssuesToPullRequest")
    .WithCriteria(() => IssuesParameters.PullRequestSystem.ShouldReportIssuesToPullRequest, "Reporting of issues to pull requests is disabled")
    .WithCriteria<IssuesData>((context, data) => data.IsPullRequestBuild, "Not a pull request build")
    .IsDependentOn("Read-Issues")
    .Does<IssuesData>((data) =>
{
    if (data.PullRequestSystem == null)
    {
        Information("Not supported pull request system.");
        return;
    }

    data.PullRequestSystem.ReportIssuesToPullRequest(Context, data);
});

IssuesBuildTasks.SetPullRequestIssuesStateTask = Task("Set-PullRequestIssuesState")
    .WithCriteria(() => IssuesParameters.PullRequestSystem.ShouldSetPullRequestStatus, "Setting of pull request status is disabled")
    .WithCriteria<IssuesData>((context, data) => data.IsPullRequestBuild, "Not a pull request build")
    .IsDependentOn("Read-Issues")
    .Does<IssuesData>((data) =>
{
    if (data.PullRequestSystem == null)
    {
        Information("Not supported pull request system.");
        return;
    }

    data.PullRequestSystem.SetPullRequestIssuesState(Context, data);
});

#load tasks/tasks.cake