using System.Reflection;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginBackgroundJobContractsTests
{
    [Fact]
    public void IPluginBackgroundJob_HasExecuteAsyncMethod()
    {
        var type = typeof(IPluginBackgroundJob);

        var method = type.GetMethod(
            "ExecuteAsync",
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(IPluginHostContext), typeof(CancellationToken)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
    }

    [Fact]
    public void IPluginBackgroundJobRunner_HasStartJobsForModuleAsync()
    {
        var type = typeof(IPluginBackgroundJobRunner);

        var method = type.GetMethod(
            "StartJobsForModuleAsync",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
    }

    [Fact]
    public void IPluginBackgroundJobRunner_HasStopJobsForModuleAsync()
    {
        var type = typeof(IPluginBackgroundJobRunner);

        var method = type.GetMethod(
            "StopJobsForModuleAsync",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
    }

    [Fact]
    public void IPluginBackgroundJobRunner_HasGetJobStatuses()
    {
        var type = typeof(IPluginBackgroundJobRunner);

        var method = type.GetMethod(
            "GetJobStatuses",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
    }

    [Fact]
    public void PluginJobStatus_HasAllSixDeclaredProperties()
    {
        var type = typeof(PluginJobStatus);

        var expectedProperties = new[]
        {
            "ModuleId",
            "JobKey",
            "JobType",
            "State",
            "LastRunUtc",
            "LastError"
        };

        foreach (var name in expectedProperties)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(prop);
        }
    }

    [Fact]
    public void PluginJobRunState_HasExactlyFiveDeclaredValues()
    {
        var values = Enum.GetNames(typeof(PluginJobRunState));

        Assert.Equal(5, values.Length);
        Assert.Contains("Idle", values);
        Assert.Contains("Running", values);
        Assert.Contains("Completed", values);
        Assert.Contains("Faulted", values);
        Assert.Contains("Stopped", values);
    }

    [Fact]
    public void PluginJobStatus_IsSealed()
    {
        Assert.True(typeof(PluginJobStatus).IsSealed);
    }
}
