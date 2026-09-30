using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public sealed class SqlProjectClassifierTests
{
    [Fact]
    public void Classifies_sdk_style_microsoft_build_sql()
    {
        const string contents = """
            <Project Sdk="Microsoft.Build.Sql/2.3.0">
              <PropertyGroup>
                <Name>Sample.Database</Name>
                <DSP>Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider</DSP>
              </PropertyGroup>
            </Project>
            """;

        Assert.Equal(SqlProjectKind.SdkStyle, SqlProjectClassifier.Classify(contents));
    }

    [Fact]
    public void Classifies_unversioned_sdk_style()
    {
        const string contents = """
            <Project Sdk="Microsoft.Build.Sql">
              <PropertyGroup><Name>Monolith.Database.Foundation</Name></PropertyGroup>
            </Project>
            """;

        Assert.Equal(SqlProjectKind.SdkStyle, SqlProjectClassifier.Classify(contents));
    }

    [Fact]
    public void Classifies_classic_ssdt()
    {
        const string contents = """
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Name>ShowcaseDataService.Database</Name>
                <OutputType>Database</OutputType>
                <DSP>Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider</DSP>
              </PropertyGroup>
              <Import Project="$(MSBuildExtensionsPath)\Microsoft\VisualStudio\v$(VisualStudioVersion)\SSDT\Microsoft.Data.Tools.Schema.SqlTasks.targets" />
            </Project>
            """;

        Assert.Equal(SqlProjectKind.ClassicSsdt, SqlProjectClassifier.Classify(contents));
    }
}
