﻿using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using Xunit;
using TechTalk.SpecFlow.Generator;
using TechTalk.SpecFlow.Generator.Interfaces;
using TechTalk.SpecFlow.GeneratorTests.Helper;
using TechTalk.SpecFlow.Utils;

namespace TechTalk.SpecFlow.GeneratorTests
{
    
    public class TestGeneratorBasicsTests : TestGeneratorTestsBase
    {
        private string GenerateTestFromSimpleFeature(ProjectSettings projectSettings, string projectRelativeFolderPath = null)
        {
            var testGenerator = CreateTestGenerator(projectSettings);

            var result = testGenerator.GenerateTestFile(CreateSimpleValidFeatureFileInput(projectRelativeFolderPath), defaultSettings);
            result.Success.Should().Be(true);
            return result.GeneratedTestCode;
        }

        [Fact]
        public void Should_generate_a_net35_csharp_test_from_simple_feature()
        {
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings);
            outputFile.Should().NotBeEmpty();
        }

        [Fact]
        public void Should_generate_a_net35_vb_test_from_simple_feature()
        {
            string outputFile = GenerateTestFromSimpleFeature(net35VBProjectSettings);
            outputFile.Should().NotBeEmpty();
        }

        [Fact]
        public void Should_include_header_in_generated_file()
        {
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings);
            outputFile.Should().Contain("This code was generated by SpecFlow");
        }

        [Fact]
        public void Should_wrap_generated_test_with_designer_region()
        {
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings);
            outputFile.Should().Contain("#region Designer generated code");
            outputFile.Should().Contain("#endregion");
        }

        [Fact]
        public void Should_include_generator_version_in_the_header()
        {
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings);
            outputFile.Should().Contain(string.Format("SpecFlow Generator Version:{0}", TestGeneratorFactory.GeneratorVersion));
        }

        [Fact]
        public void Should_include_namespace_declaration_using_default_namespace_when_file_in_project_root()
        {
            net35CSProjectSettings.DefaultNamespace = "Default.TestNamespace";
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings);
            outputFile.Should().Contain(string.Format("namespace {0}", net35CSProjectSettings.DefaultNamespace));
        }

        [Fact]
        public void Should_include_namespace_declaration_using_default_namespace_and_folder_path_when_file_in_subfolder()
        {
            net35CSProjectSettings.DefaultNamespace = "Default.TestNamespace";
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings, @"Folder1\Folder2");
            outputFile.Should().Contain(string.Format("namespace {0}.Folder1.Folder2", net35CSProjectSettings.DefaultNamespace));
        }

        [Fact]
        public void Should_include_namespace_declaration_using_fallback_namespace_when_default_namespace_not_set_and_file_in_project_root()
        {
            net35CSProjectSettings.DefaultNamespace = null;
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings);
            outputFile.Should().Contain("namespace SpecFlow.GeneratedTests");
        }

        [Fact]
        public void Should_include_namespace_declaration_using_folder_path_when_default_namespace_not_set_and_file_in_subfolder()
        {
            net35CSProjectSettings.DefaultNamespace = null;
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings, @"Folder1\Folder2");
            outputFile.Should().Contain(string.Format("namespace Folder1.Folder2", net35CSProjectSettings.DefaultNamespace));
        }

        [Fact]
        public void Should_generate_test_from_feature_file_specified_by_path()
        {
            using (var tempFile = new TempFile(".feature"))
            {
                tempFile.SetContent(CreateSimpleValidFeatureFileInput().FeatureFileContent);

                ProjectSettings projectSettings = new ProjectSettings { ProjectFolder = tempFile.FolderName, ProjectPlatformSettings = net35CSSettings };
                var testGenerator = CreateTestGenerator(projectSettings);

                var result = testGenerator.GenerateTestFile(
                    new FeatureFileInput(tempFile.FileName),
                    defaultSettings);
                result.Success.Should().Be(true);
            }
        }

        [Fact]
        public void Should_return_detected_version()
        {
            Version version = new Version();
            TestHeaderWriterStub.Setup(thw => thw.DetectGeneratedTestVersion("any")).Returns(version);

            var testGenerator = CreateTestGenerator();
            FeatureFileInput featureFileInput = CreateSimpleValidFeatureFileInput();
            featureFileInput.GeneratedTestFileContent = "any";
            var result = testGenerator.DetectGeneratedTestVersion(featureFileInput);

            result.Should().NotBeNull();
            result.Should().Be(version);
        }

        [Fact]
        public void Should_return_detected_version_from_file()
        {
            Version version = new Version();
            TestHeaderWriterStub.Setup(thw => thw.DetectGeneratedTestVersion("any")).Returns(version);

            using (var tempFile = new TempFile(".cs"))
            {
                tempFile.SetContent("any");

                ProjectSettings projectSettings = new ProjectSettings { ProjectFolder = tempFile.FolderName, ProjectPlatformSettings = net35CSSettings };
                var testGenerator = CreateTestGenerator(projectSettings);
                FeatureFileInput featureFileInput = CreateSimpleValidFeatureFileInput();
                featureFileInput.GeneratedTestProjectRelativePath = tempFile.FileName;
                var result = testGenerator.DetectGeneratedTestVersion(featureFileInput);

                result.Should().NotBeNull();
                result.Should().Be(version);
            }
        }

        [Fact]
        public void Should_return_unknown_version_when_there_is_an_error()
        {
            TestHeaderWriterStub.Setup(thw => thw.DetectGeneratedTestVersion("any")).Throws(new Exception());

            var testGenerator = CreateTestGenerator();
            FeatureFileInput featureFileInput = CreateSimpleValidFeatureFileInput();
            featureFileInput.GeneratedTestFileContent = "any";
            var result = testGenerator.DetectGeneratedTestVersion(featureFileInput);

            result.Should().Be(null);
        }

        [Fact]
        public void Should_detect_up_to_date_test_file_based_on_preliminary_up_to_date_check()
        {
            var testGenerator = CreateTestGenerator(net35CSProjectSettings);

            TestUpToDateCheckerStub.Setup(tu2d => tu2d.IsUpToDatePreliminary(It.IsAny<FeatureFileInput>(), It.IsAny<string>(), It.IsAny<UpToDateCheckingMethod>()))
                .Returns(true);

            var result = testGenerator.GenerateTestFile(CreateSimpleValidFeatureFileInput(), new GenerationSettings
                                                                                                 {
                                                                                                     CheckUpToDate = true
                                                                                                 });
            result.IsUpToDate.Should().Be(true);
        }

        [Fact]
        public void Should_detect_outdated_test_file_based_on_preliminary_up_to_date_check()
        {
            var testGenerator = CreateTestGenerator(net35CSProjectSettings);

            TestUpToDateCheckerStub.Setup(tu2d => tu2d.IsUpToDatePreliminary(It.IsAny<FeatureFileInput>(), It.IsAny<string>(), It.IsAny<UpToDateCheckingMethod>()))
                .Returns(false);

            var result = testGenerator.GenerateTestFile(CreateSimpleValidFeatureFileInput(), new GenerationSettings
                                                                                                 {
                                                                                                     CheckUpToDate = true
                                                                                                 });
            result.IsUpToDate.Should().Be(false);
        }

        [Fact]
        public void Should_detect_up_to_date_test_file_based_on_context_based_up_to_date_check()
        {
            var testGenerator = CreateTestGenerator(net35CSProjectSettings);

            TestUpToDateCheckerStub.Setup(tu2d => tu2d.IsUpToDatePreliminary(It.IsAny<FeatureFileInput>(), It.IsAny<string>(), It.IsAny<UpToDateCheckingMethod>()))
                .Returns((bool?)null);

            TestUpToDateCheckerStub.Setup(tu2d => tu2d.IsUpToDate(It.IsAny<FeatureFileInput>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpToDateCheckingMethod>()))
                .Returns(true);

            var result = testGenerator.GenerateTestFile(CreateSimpleValidFeatureFileInput(), new GenerationSettings
            {
                CheckUpToDate = true
            });
            result.IsUpToDate.Should().Be(true);
            result.GeneratedTestCode.Should().BeNull();
        }

        [Fact]
        public void Should_detect_outdated_test_file_based_on_context_based_up_to_date_check()
        {
            var testGenerator = CreateTestGenerator(net35CSProjectSettings);

            TestUpToDateCheckerStub.Setup(tu2d => tu2d.IsUpToDatePreliminary(It.IsAny<FeatureFileInput>(), It.IsAny<string>(), It.IsAny<UpToDateCheckingMethod>()))
                .Returns((bool?)null);

            TestUpToDateCheckerStub.Setup(tu2d => tu2d.IsUpToDate(It.IsAny<FeatureFileInput>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpToDateCheckingMethod>()))
                .Returns(false);

            var result = testGenerator.GenerateTestFile(CreateSimpleValidFeatureFileInput(), new GenerationSettings
            {
                CheckUpToDate = true
            });
            result.IsUpToDate.Should().Be(false);
        }

        private static string AssertFolderPathArgument(string outputFile)
        {
            var match = Regex.Match(outputFile, @"new TechTalk.SpecFlow.FeatureInfo\([^;]*");
            match.Success.Should().BeTrue("FeatureInfo ctor should be found in output");
            var folderPathArgument = match.Value.Split(',')[1].Trim();
            folderPathArgument.Should().StartWith("\"").And.EndWith("\"", "the folderPath argument should be a string");
            return folderPathArgument;
        }

        [Fact]
        public void Should_generate_empty_folderpath_when_file_in_project_root()
        {
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings);

            string folderPathArgument = AssertFolderPathArgument(outputFile);

            folderPathArgument.Should().Be("\"\"");
        }

        [Fact]
        public void Should_generate_folderpath_with_slash_separator_when_file_in_subfolder()
        {
            string outputFile = GenerateTestFromSimpleFeature(net35CSProjectSettings, Path.Combine("Folder1", "Folder2"));

            string folderPathArgument = AssertFolderPathArgument(outputFile);

            folderPathArgument.Should().Be("\"Folder1/Folder2\"");
        }
    }
}
