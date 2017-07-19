﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NET462
using System.Runtime.Loader;
#endif
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace netmockery
{
    public abstract class DynamicResponseCreatorBase : SimpleResponseCreator
    {
        public DynamicResponseCreatorBase(Endpoint endpoint) : base(endpoint) { }

        public virtual string FileSystemDirectory { get { return null; } }

        public async Task<string> EvaluateAsync(RequestInfo requestInfo)
        {
            //TODO: Only create script object if source has changed
            Debug.Assert(requestInfo != null);
            
            var scriptOptions = ScriptOptions.Default.WithReferences(
                MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location), // System.Linq
                MetadataReference.CreateFromFile(typeof(System.Xml.Linq.XElement).GetTypeInfo().Assembly.Location), // System.Xml.Linq
                MetadataReference.CreateFromFile(typeof(System.IO.File).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Diagnostics.Debug).GetTypeInfo().Assembly.Location)
            );

            var script = CSharpScript.Create<string>(
                FileSystemDirectory != null 
                    ?
                    ExecuteIncludes(
                        CreateCorrectPathsInLoadStatements(SourceCode, FileSystemDirectory),
                        FileSystemDirectory
                    )                        
                    : 
                    SourceCode,
                scriptOptions,
                globalsType: typeof(RequestInfo)
            );
            var result = await script.RunAsync(globals: requestInfo);
            return result.ReturnValue;
        }

        static public string CreateCorrectPathsInLoadStatements(string sourceCode, string directory)
        {
            Debug.Assert(sourceCode != null);
            Debug.Assert(directory != null);
            return Regex.Replace(
                sourceCode, 
                "#load \"(.*?)\"",
                mo => "#load \"" + Path.GetFullPath(Path.Combine(directory, mo.Groups[1].Value)) + "\""
            );
        }

        static public string ExecuteIncludes(string sourceCode, string directory)
        {
            Debug.Assert(sourceCode != null);
            Debug.Assert(directory != null);

            return Regex.Replace(
                sourceCode,
                "#include \"(.*?)\"",
                mo => File.ReadAllText(Path.GetFullPath(Path.Combine(directory, mo.Groups[1].Value)))
            );

        }

        public override Task<string> GetBodyAsync(RequestInfo requestInfo) => EvaluateAsync(requestInfo);

        public abstract string SourceCode { get; }
    }

    public class LiteralDynamicResponseCreator : DynamicResponseCreatorBase
    {
        private string _sourceCode;

        public LiteralDynamicResponseCreator(string sourceCode, Endpoint endpoint) : base(endpoint)
        {
            _sourceCode = sourceCode;
        }

        public override string SourceCode => _sourceCode;
    }

    public class FileDynamicResponseCreator : DynamicResponseCreatorBase, IResponseCreatorWithFilename
    {
        private string _filename;

        public FileDynamicResponseCreator(string filename, Endpoint endpoint) : base(endpoint)
        {
            _filename = filename;
        }

        public string Filename => Path.Combine(Endpoint.Directory, ReplaceParameterReference(_filename));

        public override string SourceCode => File.ReadAllText(Filename);

        public override string FileSystemDirectory => Path.GetDirectoryName(Filename);

        public override string ToString() => $"Execute script {ReplaceParameterReference(_filename)}";
    }
}
