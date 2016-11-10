﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using netmockery;

namespace UnitTests
{
    public static class TESTCOMMAND_CONSTANTS
    {
        public const string ENDPOINTJSON = @"
{
    'name': 'foo',
    'pathregex': '^/foo/$',
    'responses': [
        {
            'match': {'regex': 'test'},
            'response': {
                'file': 'content.txt',
                'contenttype': 'text/plain'
            }
        },
        {
            'match': {},
            'response': {
                'script': 'myscript.csscript',
                'contenttype': 'text/xml',
                'replacements': [
                    {'search': 'a', 'replace': 'b'},
                    {'search': 'foo', 'replace': 'bar'}
                ]
            }
        }
    ]
}
";

        public const string TESTS = @"
[
    {
        'name': '/foo/ request works',
        'requestpath': '/foo/',
        'requestbody': 'heisann test',
        'expectedresponsebody': 'FOOBARBOOBAR'
    },

    {
        'name': '/foo/ request works',
        'requestpath': '/foo/',
        'requestbody': 'heisann test',
        'expectedresponsebody': 'file:example.txt'
    },

    {
        'name': '/foo/ request works',
        'requestpath': '/foo/',
        'requestbody': 'file:example.txt',
        'expectedresponsebody': 'file:example.txt',
    },

]
";

    }

    public class TestTestCommandWithoutTestsuite : IDisposable
    {
        DirectoryCreator dc;
        public TestTestCommandWithoutTestsuite()
        {
            dc = new DirectoryCreator();
            dc.AddFile("endpoint1\\endpoint.json", TESTCOMMAND_CONSTANTS.ENDPOINTJSON);
        }

        public void Dispose()
        {
            dc.Dispose();
        }
        
        [Fact]
        public void CheckIfTestSuiteExists()
        {
            Assert.False(EndpointTestDefinition.HasTestSuite(dc.DirectoryName));
        }

        [Fact]
        public void WorksIfEndpointNamedTest()
        {
            dc.AddFile("tests\\endpoint.json", TESTCOMMAND_CONSTANTS.ENDPOINTJSON);
            Assert.False(EndpointTestDefinition.HasTestSuite(dc.DirectoryName));
        }
    }


    public class TestTestCommand : IDisposable
    {
        DirectoryCreator dc;
        public TestTestCommand()
        {
            dc = new DirectoryCreator();
            dc.AddFile("endpoint1\\endpoint.json", TESTCOMMAND_CONSTANTS.ENDPOINTJSON);
            dc.AddFile("endpoint1\\content.txt", "FOOBARBOOBAR");
            dc.AddFile("tests\\tests.json", TESTCOMMAND_CONSTANTS.TESTS);
            dc.AddFile("tests\\example.txt", "FOOBARBOOBAR");
        }

        public void Dispose()
        {
            dc.Dispose();
        }

        [Fact]
        public void DetectsTestSuite()
        {
            Assert.True(EndpointTestDefinition.HasTestSuite(dc.DirectoryName));

        }

        [Fact]
        public void CanReadTestsFromJSONFile()
        {
            var endpointTestDefinition = EndpointTestDefinition.ReadFromDirectory(dc.DirectoryName);
            Assert.Equal(3, endpointTestDefinition.Tests.Count());
            var test = endpointTestDefinition.Tests.ElementAt(0);
            Assert.Equal("/foo/ request works", test.Name);
            Assert.Equal("/foo/", test.RequestPath);
            Assert.Equal("heisann test", test.RequestBody);
            Assert.Equal("FOOBARBOOBAR", test.ExpectedResponseBody);
        }

        [Fact]
        public void RequestBodyCanBeReadFromFile()
        {
            var endpointTestDefinition = EndpointTestDefinition.ReadFromDirectory(dc.DirectoryName);
            var test = endpointTestDefinition.Tests.ElementAt(2);
            Assert.Equal("FOOBARBOOBAR", test.RequestBody);
        }

        [Fact]
        async public void CanExecuteTest()
        {
            var endpointTestDefinition = EndpointTestDefinition.ReadFromDirectory(dc.DirectoryName);
            var test = endpointTestDefinition.Tests.ElementAt(0);

            var result = await test.ExecuteAsync(EndpointCollectionReader.ReadFromDirectory(dc.DirectoryName), handleErrors: false);
            Assert.True(result.OK);
        }

        [Fact]
        async public void CanReadExpectedResponseBodyFromFile()
        {
            var endpointTestDefinition = EndpointTestDefinition.ReadFromDirectory(dc.DirectoryName);
            var test = endpointTestDefinition.Tests.ElementAt(1);

            var result = await test.ExecuteAsync(EndpointCollectionReader.ReadFromDirectory(dc.DirectoryName), handleErrors: false);
            Assert.True(result.OK, result.Message);
        }

        [Fact]
        async public void CanCheckExpectedRequestMatcherError()
        {
            var testcase = 
                (new JSONTest { name="checksomething", requestpath = "/foo/", requestbody = "foobar", expectedrequestmatcher = "Regex 'test'" })
                .Validated().CreateTestCase(".");

            Assert.True(testcase.HasExpectations);
            Assert.False(testcase.NeedsResponseBody);
            Assert.Equal("Regex 'test'", testcase.ExpectedRequestMatcher);
            Assert.Equal("foobar", testcase.RequestBody);

            var result = await testcase.ExecuteAsync(EndpointCollectionReader.ReadFromDirectory(dc.DirectoryName));
            Assert.True(result.Error);
            Assert.Null(result.Exception);
            Assert.Equal("Expected request matcher: Regex 'test'\nActual: Any request", result.Message);
        }

        [Fact]
        async public void CanCheckExpectedRequestMatcherSuccess()
        {
            var testcase =
                (new JSONTest { name = "checksomething", requestpath = "/foo/", requestbody = "this is a test", expectedrequestmatcher = "Regex 'test'" })
                .Validated().CreateTestCase(".");
            var result = await testcase.ExecuteAsync(EndpointCollectionReader.ReadFromDirectory(dc.DirectoryName));
            Assert.True(result.OK);
        }

        [Fact]
        async public void CanCheckExpectedResponseCreatorError()
        {
            var testcase =
                (new JSONTest { name = "checksomething", requestpath = "/foo/", requestbody = "foobar", expectedresponsecreator = "File content.txt" })
                .Validated().CreateTestCase(".");
            Assert.True(testcase.HasExpectations);
            Assert.False(testcase.NeedsResponseBody);
            Assert.Equal("File content.txt", testcase.ExpectedResponseCreator);
            var result = await testcase.ExecuteAsync(EndpointCollectionReader.ReadFromDirectory(dc.DirectoryName));
            Assert.True(result.Error);
            Assert.Equal("Expected response creator: File content.txt\nActual: Execute script myscript.csscript", result.Message);
        }

        [Fact]
        async public void CanCheckExpectedResponseCreatorSuccess()
        {
            var testcase =
                (new JSONTest { name = "checksomething", requestpath = "/foo/", requestbody = "this is a test", expectedresponsecreator = "File content.txt" })
                .Validated().CreateTestCase(".");
            Assert.Equal("File content.txt", testcase.ExpectedResponseCreator);
            var result = await testcase.ExecuteAsync(EndpointCollectionReader.ReadFromDirectory(dc.DirectoryName));
            Assert.True(result.OK, result.Message);
            Assert.Null(result.Message);
        }

        [Fact]
        public void CanReadQueryString()
        {
            var testcase = (new JSONTest { querystring = "?foo=bar" }).CreateTestCase(".");
            Assert.Equal("?foo=bar", testcase.QueryString);
        }

        [Fact]
        async public void CanExecuteWithQueryStringFailure()
        {
            var testcase =
                (new JSONTest { name = "checksomething", requestpath = "/foo/", querystring = "?a=test", requestbody = "foobar", expectedresponsecreator = "File content.txt" })
                .Validated().CreateTestCase(".");
            var result = await testcase.ExecuteAsync(EndpointCollectionReader.ReadFromDirectory(dc.DirectoryName));
            Assert.True(result.OK, result.Message);
            Assert.Null(result.Message);
        }
    }
}
