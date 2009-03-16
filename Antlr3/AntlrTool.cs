﻿/*
 * [The "BSD licence"]
 * Copyright (c) 2005-2008 Terence Parr
 * All rights reserved.
 *
 * Conversion to C#:
 * Copyright (c) 2008 Sam Harwell, Pixel Mine, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace Antlr3
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr.Runtime.JavaExtensions;
    using Antlr3.Analysis;
    using Antlr3.Codegen;
    using Antlr3.Tool;

    using File = System.IO.File;
    using FileInfo = System.IO.FileInfo;
    using Graph = Antlr3.Misc.Graph;
    using IList = System.Collections.IList;
    using IOException = System.IO.IOException;
    using Path = System.IO.Path;
    using Stats = Antlr.Runtime.Misc.Stats;
    using StringReader = System.IO.StringReader;
    using StringWriter = System.IO.StringWriter;
    using TextWriter = System.IO.TextWriter;

    public class AntlrTool
    {
        public string VERSION = "3.1.2";
        public const string UNINITIALIZED_DIR = "<unset-dir>";
        private HashSet<string> grammarFileNames = new HashSet<string>();
        private bool generate_NFA_dot = false;
        private bool generate_DFA_dot = false;
        private string outputDirectory = ".";
        private bool haveOutputDir = false;
        private string inputDirectory = "";
        private string parentGrammarDirectory;
        private string grammarOutputDirectory;
        private bool haveInputDir = false;
        private string libDirectory = ".";
        private bool debug = false;
        private bool trace = false;
        private bool profile = false;
        private bool report = false;
        private bool printGrammar = false;
        private bool depend = false;
        private bool forceAllFilesToOutputDir = false;
        private bool forceRelativeOutput = false;
        private bool deleteTempLexer = true;
        private bool verbose = false;
        /** Don't process grammar file if generated files are newer than grammar */
        private bool make = false;
        private bool showBanner = true;
        // true when we are in a unit test
        private bool testMode = false;
        private static bool exitNow = false;

        // The internal options are for my use on the command line during dev
        //
        public static bool internalOption_PrintGrammarTree = false;
        public static bool internalOption_PrintDFA = false;
        public static bool internalOption_ShowNFAConfigsInDFA = false;
        public static bool internalOption_watchNFAConversion = false;

#if false
        /**
         * A list of dependency generators that are accumulated aaaas (and if) the
         * tool is required to sort the provided grammars into build dependency order.
         */
        protected Dictionary<string, BuildDependencyGenerator> buildDependencyGenerators;
#endif

        public static void Main( string[] args )
        {
            AntlrTool antlr = new AntlrTool( args );
            if ( !exitNow )
            {
                antlr.process();
                Environment.ExitCode = ( ErrorManager.getNumErrors() > 0 ) ? 1 : 0;
            }
        }

        public AntlrTool()
        {
        }

        public AntlrTool( string[] args )
        {
            processArgs( args );
        }

        public virtual void processArgs( string[] args )
        {
            if ( verbose )
            {
                ErrorManager.info( "ANTLR Parser Generator  Version " + VERSION );
                showBanner = false;
            }

            if ( args == null || args.Length == 0 )
            {
                help();
                return;
            }

            for ( int i = 0; i < args.Length; i++ )
            {
                if ( args[i] == "-o" || args[i] == "-fo" )
                {
                    if ( i + 1 >= args.Length )
                    {
                        Console.Error.WriteLine( "missing output directory with -fo/-o option; ignoring" );
                    }
                    else
                    {
                        if ( args[i] == "-fo" )
                            ForceAllFilesToOutputDir = true;
                        i++;
                        outputDirectory = args[i];
                        if ( outputDirectory.EndsWith( "/" ) || outputDirectory.EndsWith( "\\" ) )
                            outputDirectory = outputDirectory.Substring( 0, OutputDirectory.Length - 1 );
                        haveOutputDir = true;
                        if ( System.IO.File.Exists( outputDirectory ) )
                        {
                            ErrorManager.error( ErrorManager.MSG_OUTPUT_DIR_IS_FILE, outputDirectory );
                            LibraryDirectory = ".";
                        }
                    }
                }
                else if ( args[i] == "-lib" )
                {
                    if ( i + 1 >= args.Length )
                    {
                        Console.Error.WriteLine( "missing library directory with -lib option; ignoring" );
                    }
                    else
                    {
                        i++;
                        LibraryDirectory = args[i];
                        if ( LibraryDirectory.EndsWith( "/" ) ||
                             LibraryDirectory.EndsWith( "\\" ) )
                        {
                            LibraryDirectory =
                                LibraryDirectory.Substring( 0, LibraryDirectory.Length - 1 );
                        }
                        if ( !System.IO.Directory.Exists( libDirectory ) )
                        {
                            ErrorManager.error( ErrorManager.MSG_DIR_NOT_FOUND, LibraryDirectory );
                            LibraryDirectory = ".";
                        }
                    }
                }
                else if ( args[i] == "-nfa" )
                {
                    Generate_NFA_dot = true;
                }
                else if ( args[i] == "-dfa" )
                {
                    Generate_DFA_dot = true;
                }
                else if ( args[i] == "-debug" )
                {
                    Debug = true;
                }
                else if ( args[i] == "-trace" )
                {
                    Trace = true;
                }
                else if ( args[i] == "-report" )
                {
                    Report = true;
                }
                else if ( args[i] == "-profile" )
                {
                    Profile = true;
                }
                else if ( args[i] == "-print" )
                {
                    PrintGrammar = true;
                }
                else if ( args[i] == "-depend" )
                {
                    Depend = true;
                }
                else if ( args[i] == "-testmode" )
                {
                    TestMode = true;
                }
                else if ( args[i] == "-verbose" )
                {
                    Verbose = true;
                }
                else if ( args[i] == "-version" )
                {
                    version();
                    exitNow = true;
                }
                else if ( args[i] == "-make" )
                {
                    Make = true;
                }
                else if ( args[i] == "-message-format" )
                {
                    if ( i + 1 >= args.Length )
                    {
                        Console.Error.WriteLine( "missing output format with -message-format option; using default" );
                    }
                    else
                    {
                        i++;
                        ErrorManager.setFormat( args[i] );
                    }
                }
                else if ( args[i] == "-Xgrtree" )
                {
                    internalOption_PrintGrammarTree = true;
                }
                else if ( args[i] == "-Xdfa" )
                {
                    internalOption_PrintDFA = true;
                }
                else if ( args[i] == "-Xnoprune" )
                {
                    DFAOptimizer.PRUNE_EBNF_EXIT_BRANCHES = false;
                }
                else if ( args[i] == "-Xnocollapse" )
                {
                    DFAOptimizer.COLLAPSE_ALL_PARALLEL_EDGES = false;
                }
                else if ( args[i] == "-Xdbgconversion" )
                {
                    NFAToDFAConverter.debug = true;
                }
                else if ( args[i] == "-Xmultithreaded" )
                {
                    NFAToDFAConverter.SINGLE_THREADED_NFA_CONVERSION = false;
                }
                else if ( args[i] == "-Xnomergestopstates" )
                {
                    DFAOptimizer.MERGE_STOP_STATES = false;
                }
                else if ( args[i] == "-Xdfaverbose" )
                {
                    internalOption_ShowNFAConfigsInDFA = true;
                }
                else if ( args[i] == "-Xwatchconversion" )
                {
                    internalOption_watchNFAConversion = true;
                }
                else if ( args[i] == "-XdbgST" )
                {
                    CodeGenerator.EMIT_TEMPLATE_DELIMITERS = true;
                }
                else if ( args[i] == "-Xmaxinlinedfastates" )
                {
                    if ( i + 1 >= args.Length )
                    {
                        Console.Error.WriteLine( "missing max inline dfa states -Xmaxinlinedfastates option; ignoring" );
                    }
                    else
                    {
                        i++;
                        CodeGenerator.MAX_ACYCLIC_DFA_STATES_INLINE = int.Parse( args[i] );
                    }
                }
                else if ( args[i] == "-Xm" )
                {
                    if ( i + 1 >= args.Length )
                    {
                        Console.Error.WriteLine( "missing max recursion with -Xm option; ignoring" );
                    }
                    else
                    {
                        i++;
                        NFAContext.MAX_SAME_RULE_INVOCATIONS_PER_NFA_CONFIG_STACK = int.Parse( args[i] );
                    }
                }
                else if ( args[i] == "-Xmaxdfaedges" )
                {
                    if ( i + 1 >= args.Length )
                    {
                        Console.Error.WriteLine( "missing max number of edges with -Xmaxdfaedges option; ignoring" );
                    }
                    else
                    {
                        i++;
                        DFA.MAX_STATE_TRANSITIONS_FOR_TABLE = int.Parse( args[i] );
                    }
                }
                else if ( args[i] == "-Xconversiontimeout" )
                {
                    if ( i + 1 >= args.Length )
                    {
                        Console.Error.WriteLine( "missing max time in ms -Xconversiontimeout option; ignoring" );
                    }
                    else
                    {
                        i++;
                        DFA.MAX_TIME_PER_DFA_CREATION = TimeSpan.FromMilliseconds( int.Parse( args[i] ) );
                    }
                }
                else if ( args[i] == "-Xnfastates" )
                {
                    DecisionProbe.verbose = true;
                }
                else if ( args[i] == "-X" )
                {
                    Xhelp();
                }
                else
                {
                    if ( args[i][0] != '-' )
                    {
                        // Must be the grammar file
                        addGrammarFile( args[i] );
                    }
                }
            }
        }

#if false
        protected virtual void checkForInvalidArguments( string[] args, Antlr.Runtime.BitSet cmdLineArgValid )
        {
            // check for invalid command line args
            for ( int a = 0; a < args.Length; a++ )
            {
                if ( !cmdLineArgValid.Member( a ) )
                {
                    Console.Error.WriteLine( "invalid command-line argument: " + args[a] + "; ignored" );
                }
            }
        }
#endif

        /**
         * Checks to see if the list of outputFiles all exist, and have
         * last-modified timestamps which are later than the last-modified
         * timestamp of all the grammar files involved in build the output
         * (imports must be checked). If these conditions hold, the method
         * returns false, otherwise, it returns true.
         *
         * @param grammarFileName The grammar file we are checking
         * @param outputFiles
         * @return
         */
        public virtual bool buildRequired( string grammarFileName )
        {
            BuildDependencyGenerator bd = new BuildDependencyGenerator( this, grammarFileName );
            IList<string> outputFiles = bd.getGeneratedFileList();
            IList<string> inputFiles = bd.getDependenciesFileList();
            DateTime grammarLastModified = File.GetLastWriteTime( grammarFileName );
            foreach ( string outputFile in outputFiles )
            {
                if ( !File.Exists( outputFile ) || grammarLastModified > File.GetLastWriteTime( outputFile ) )
                {
                    // One of the output files does not exist or is out of date, so we must build it
                    return true;
                }

                // Check all of the imported grammars and see if any of these are younger
                // than any of the output files.
                if ( inputFiles != null )
                {
                    foreach ( string inputFile in inputFiles )
                    {
                        if ( File.GetLastWriteTime( inputFile ) > File.GetLastWriteTime( outputFile ) )
                        {
                            // One of the imported grammar files has been updated so we must build
                            return true;
                        }
                    }
                }
            }
            if ( Verbose )
            {
                Console.Out.WriteLine( "Grammar " + grammarFileName + " is up to date - build skipped" );
            }
            return false;
        }

        public virtual void process()
        {
            bool exceptionWhenWritingLexerFile = false;
            string lexerGrammarFileName = null;		// necessary at this scope to have access in the catch below

            // Have to be tricky here when Maven or build tools call in and must new Tool()
            // before setting options. The banner won't display that way!
            if ( Verbose && showBanner )
            {
                ErrorManager.info( "ANTLR Parser Generator  Version " + VERSION );
                showBanner = false;
            }

            try
            {
                sortGrammarFiles(); // update grammarFileNames
            }
            catch ( Exception e )
            {
                ErrorManager.error( ErrorManager.MSG_INTERNAL_ERROR, e );
            }

            foreach ( string grammarFileName in GrammarFileNames )
            {
                // If we are in make mode (to support build tools like Maven) and the
                // file is already up to date, then we do not build it (and in verbose mode
                // we will say so).
                if ( Make )
                {
                    try
                    {
                        if ( !buildRequired( grammarFileName ) )
                            continue;
                    }
                    catch ( Exception e )
                    {
                        ErrorManager.error( ErrorManager.MSG_INTERNAL_ERROR, e );
                    }
                }

                if ( Verbose && !Depend )
                {
                    Console.Out.WriteLine( grammarFileName );
                }
                try
                {
                    if ( Depend )
                    {
                        BuildDependencyGenerator dep = new BuildDependencyGenerator( this, grammarFileName );
#if false
                        IList<string> outputFiles = dep.getGeneratedFileList();
                        IList<string> dependents = dep.getDependenciesFileList();
                        Console.Out.WriteLine( "output: " + outputFiles );
                        Console.Out.WriteLine( "dependents: " + dependents );
#endif
                        Console.Out.WriteLine( dep.getDependencies() );
                        continue;
                    }

                    Grammar grammar = getRootGrammar( grammarFileName );
                    // we now have all grammars read in as ASTs
                    // (i.e., root and all delegates)
                    grammar.composite.assignTokenTypes();
                    grammar.composite.defineGrammarSymbols();
                    grammar.composite.createNFAs();

                    generateRecognizer( grammar );

                    if ( PrintGrammar )
                    {
                        grammar.printGrammar( Console.Out );
                    }

                    if ( Report )
                    {
                        GrammarReport report2 = new GrammarReport( grammar );
                        Console.Out.WriteLine( report2.ToString() );
                        // print out a backtracking report too (that is not encoded into log)
                        Console.Out.WriteLine( report2.getBacktrackingReport() );
                        // same for aborted NFA->DFA conversions
                        Console.Out.WriteLine( report2.getAnalysisTimeoutReport() );
                    }
                    if ( Profile )
                    {
                        GrammarReport report2 = new GrammarReport( grammar );
                        Stats.WriteReport( GrammarReport.GRAMMAR_STATS_FILENAME,
                                          report2.toNotifyString() );
                    }

                    // now handle the lexer if one was created for a merged spec
                    string lexerGrammarStr = grammar.getLexerGrammar();
                    //JSystem.@out.println("lexer grammar:\n"+lexerGrammarStr);
                    if ( grammar.type == Grammar.COMBINED && lexerGrammarStr != null )
                    {
                        lexerGrammarFileName = grammar.ImplicitlyGeneratedLexerFileName;
                        try
                        {
                            TextWriter w = getOutputFile( grammar, lexerGrammarFileName );
                            w.Write( lexerGrammarStr );
                            w.Close();
                        }
                        catch ( IOException e )
                        {
                            // emit different error message when creating the implicit lexer fails
                            // due to write permission error
                            exceptionWhenWritingLexerFile = true;
                            throw e;
                        }
                        try
                        {
                            StringReader sr = new StringReader( lexerGrammarStr );
                            Grammar lexerGrammar = new Grammar();
                            lexerGrammar.composite.watchNFAConversion = internalOption_watchNFAConversion;
                            lexerGrammar.implicitLexer = true;
                            lexerGrammar.Tool = this;
                            if ( TestMode )
                                lexerGrammar.DefaultRuleModifier = "public";
                            FileInfo lexerGrammarFullFile = new FileInfo( System.IO.Path.Combine( getFileDirectory( lexerGrammarFileName ), lexerGrammarFileName ) );
                            lexerGrammar.FileName = lexerGrammarFullFile.ToString();

                            lexerGrammar.importTokenVocabulary( grammar );
                            lexerGrammar.parseAndBuildAST( sr );

                            sr.Close();

                            lexerGrammar.composite.assignTokenTypes();
                            lexerGrammar.composite.defineGrammarSymbols();
                            lexerGrammar.composite.createNFAs();

                            generateRecognizer( lexerGrammar );
                        }
                        finally
                        {
                            // make sure we clean up
                            if ( deleteTempLexer )
                            {
                                System.IO.DirectoryInfo outputDir = getOutputDirectory( lexerGrammarFileName );
                                FileInfo outputFile = new FileInfo( System.IO.Path.Combine( outputDir.FullName, lexerGrammarFileName ) );
                                outputFile.Delete();
                            }
                        }
                    }
                }
                catch ( IOException e )
                {
                    if ( exceptionWhenWritingLexerFile )
                    {
                        ErrorManager.error( ErrorManager.MSG_CANNOT_WRITE_FILE,
                                           lexerGrammarFileName, e );
                    }
                    else
                    {
                        ErrorManager.error( ErrorManager.MSG_CANNOT_OPEN_FILE,
                                           grammarFileName );
                    }
                }
                catch ( Exception e )
                {
                    ErrorManager.error( ErrorManager.MSG_INTERNAL_ERROR, grammarFileName, e );
                }
#if false
                finally
                {
                    Console.Out.WriteLine( "creates=" + Interval.creates );
                    Console.Out.WriteLine( "hits=" + Interval.hits );
                    Console.Out.WriteLine( "misses=" + Interval.misses );
                    Console.Out.WriteLine( "outOfRange=" + Interval.outOfRange );
                }
#endif
            }
        }

        public virtual void sortGrammarFiles()
        {
            //System.out.println("Grammar names "+getGrammarFileNames());
            Graph g = new Graph();
            foreach ( string gfile in GrammarFileNames )
            {
                GrammarSpelunker grammar = new GrammarSpelunker( gfile );
                grammar.parse();
                string vocabName = grammar.getTokenVocab();
                string grammarName = grammar.getGrammarName();
                // Make all grammars depend on any tokenVocab options
                if ( vocabName != null )
                    g.AddEdge( gfile, vocabName + CodeGenerator.VOCAB_FILE_EXTENSION );
                // Make all generated tokens files depend on their grammars
                g.AddEdge( grammarName + CodeGenerator.VOCAB_FILE_EXTENSION, gfile );
            }
            List<object> sorted = g.Sort();
            //Console.Out.WriteLine( "sorted=" + sorted );
            grammarFileNames.Clear(); // wipe so we can give new ordered list
            for ( int i = 0; i < sorted.Count; i++ )
            {
                string f = (string)sorted[i];
                if ( f.EndsWith( ".g" ) )
                    grammarFileNames.Add( f );
            }
            //Console.Out.WriteLine( "new grammars=" + grammarFileNames );
        }

        /** Get a grammar mentioned on the command-line and any delegates */
        public virtual Grammar getRootGrammar( string grammarFileName )
        {
            //StringTemplate.setLintMode(true);
            // grammars mentioned on command line are either roots or single grammars.
            // create the necessary composite in case it's got delegates; even
            // single grammar needs it to get token types.
            CompositeGrammar composite = new CompositeGrammar();
            Grammar grammar = new Grammar( this, grammarFileName, composite );
            if ( TestMode )
                grammar.DefaultRuleModifier = "public";
            composite.setDelegationRoot( grammar );
            //FileReader fr = null;
            //fr = new FileReader( grammarFileName );
            string f = null;

            if ( haveInputDir )
            {
                f = Path.Combine( inputDirectory, grammarFileName );
            }
            else
            {
                f = grammarFileName;
            }

            // Store the location of this grammar as if we import files, we can then
            // search for imports in the same location as the original grammar as well as in
            // the lib directory.
            //
            parentGrammarDirectory = Path.GetDirectoryName( f );

            if ( grammarFileName.LastIndexOfAny( new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } ) == -1 )
            {
                grammarOutputDirectory = ".";
            }
            else
            {
                grammarOutputDirectory = grammarFileName.Substring( 0, grammarFileName.LastIndexOfAny( new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } ) );
            }

            //BufferedReader br = new BufferedReader( fr );
            //grammar.parseAndBuildAST( br );
            StringReader reader = new StringReader( System.IO.File.ReadAllText( f ) );
            grammar.parseAndBuildAST( reader );
            composite.watchNFAConversion = internalOption_watchNFAConversion;
            //br.close();
            //fr.close();
            return grammar;
        }

        /** Create NFA, DFA and generate code for grammar.
         *  Create NFA for any delegates first.  Once all NFA are created,
         *  it's ok to create DFA, which must check for left-recursion.  That check
         *  is done by walking the full NFA, which therefore must be complete.
         *  After all NFA, comes DFA conversion for root grammar then code gen for
         *  root grammar.  DFA and code gen for delegates comes next.
         */
        protected virtual void generateRecognizer( Grammar grammar )
        {
            string language = (string)grammar.getOption( "language" );
            if ( language != null )
            {
                CodeGenerator generator = new CodeGenerator( this, grammar, language );
                grammar.setCodeGenerator( generator );
                generator.setDebug( Debug );
                generator.setProfile( Profile );
                generator.setTrace( Trace );

                // generate NFA early in case of crash later (for debugging)
                if ( Generate_NFA_dot )
                {
                    generateNFAs( grammar );
                }

                // GENERATE CODE
                generator.genRecognizer();

                if ( Generate_DFA_dot )
                {
                    generateDFAs( grammar );
                }

                IList<Grammar> delegates = grammar.getDirectDelegates();
                for ( int i = 0; delegates != null && i < delegates.Count; i++ )
                {
                    Grammar @delegate = (Grammar)delegates[i];
                    if ( @delegate != grammar )
                    { // already processing this one
                        generateRecognizer( @delegate );
                    }
                }
            }
        }

        public virtual void generateDFAs( Grammar g )
        {
            for ( int d = 1; d <= g.NumberOfDecisions; d++ )
            {
                DFA dfa = g.getLookaheadDFA( d );
                if ( dfa == null )
                {
                    continue; // not there for some reason, ignore
                }
                DOTGenerator dotGenerator = new DOTGenerator( g );
                string dot = dotGenerator.getDOT( dfa.startState );
                string dotFileName = g.name + "." + "dec-" + d;
                if ( g.implicitLexer )
                {
                    dotFileName = g.name + Grammar.grammarTypeToFileNameSuffix[g.type] + "." + "dec-" + d;
                }
                try
                {
                    writeDOTFile( g, dotFileName, dot );
                }
                catch ( IOException ioe )
                {
                    ErrorManager.error( ErrorManager.MSG_CANNOT_GEN_DOT_FILE,
                                       dotFileName,
                                       ioe );
                }
            }
        }

        protected virtual void generateNFAs( Grammar g )
        {
            DOTGenerator dotGenerator = new DOTGenerator( g );
            ICollection<Rule> rules = g.getAllImportedRules();
            rules.addAll( g.Rules );

            foreach ( Rule r in rules )
            {
                try
                {
                    string dot = dotGenerator.getDOT( r.startState );
                    if ( dot != null )
                    {
                        writeDOTFile( g, r, dot );
                    }
                }
                catch ( IOException ioe )
                {
                    ErrorManager.error( ErrorManager.MSG_CANNOT_WRITE_FILE, ioe );
                }
            }
        }

        protected virtual void writeDOTFile( Grammar g, Rule r, string dot )
        {
            writeDOTFile( g, r.grammar.name + "." + r.name, dot );
        }

        protected virtual void writeDOTFile( Grammar g, string name, string dot )
        {
            TextWriter fw = getOutputFile( g, name + ".dot" );
            fw.Write( dot );
            fw.Close();
        }

        private static void version()
        {
            ErrorManager.info( "ANTLR Parser Generator  Version " + new AntlrTool().VERSION );
        }

        private static void help()
        {
            version();
            Console.Error.WriteLine( "usage: java org.antlr.Tool [args] file.g [file2.g file3.g ...]" );
            Console.Error.WriteLine( "  -o outputDir          specify output directory where all output is generated" );
            Console.Error.WriteLine( "  -fo outputDir         same as -o but force even files with relative paths to dir" );
            Console.Error.WriteLine( "  -lib dir              specify location of token files" );
            Console.Error.WriteLine( "  -depend               generate file dependencies" );
            Console.Error.WriteLine( "  -verbose              generate ANTLR version and other information" );
            Console.Error.WriteLine( "  -report               print out a report about the grammar(s) processed" );
            Console.Error.WriteLine( "  -print                print out the grammar without actions" );
            Console.Error.WriteLine( "  -debug                generate a parser that emits debugging events" );
            Console.Error.WriteLine( "  -profile              generate a parser that computes profiling information" );
            Console.Error.WriteLine( "  -nfa                  generate an NFA for each rule" );
            Console.Error.WriteLine( "  -dfa                  generate a DFA for each decision point" );
            Console.Error.WriteLine( "  -message-format name  specify output style for messages" );
            Console.Error.WriteLine( "  -verbose              generate ANTLR version and other information" );
            Console.Error.WriteLine( "  -make                 only build if generated files older than grammar" );
            Console.Error.WriteLine( "  -version              print the version of ANTLR and exit." );
            Console.Error.WriteLine( "  -X                    display extended argument list" );
        }

        private static void Xhelp()
        {
            version();
            Console.Error.WriteLine( "  -Xgrtree               print the grammar AST" );
            Console.Error.WriteLine( "  -Xdfa                  print DFA as text " );
            Console.Error.WriteLine( "  -Xnoprune              test lookahead against EBNF block exit branches" );
            Console.Error.WriteLine( "  -Xnocollapse           collapse incident edges into DFA states" );
            Console.Error.WriteLine( "  -Xdbgconversion        dump lots of info during NFA conversion" );
            Console.Error.WriteLine( "  -Xmultithreaded        run the analysis in 2 threads" );
            Console.Error.WriteLine( "  -Xnomergestopstates    do not merge stop states" );
            Console.Error.WriteLine( "  -Xdfaverbose           generate DFA states in DOT with NFA configs" );
            Console.Error.WriteLine( "  -Xwatchconversion      print a message for each NFA before converting" );
            Console.Error.WriteLine( "  -XdbgST                put tags at start/stop of all templates in output" );
            Console.Error.WriteLine( "  -Xm m                  max number of rule invocations during conversion" );
            Console.Error.WriteLine( "  -Xmaxdfaedges m        max \"comfortable\" number of edges for single DFA state" );
            Console.Error.WriteLine( "  -Xconversiontimeout t  set NFA conversion timeout for each decision" );
            Console.Error.WriteLine( "  -Xmaxinlinedfastates m max DFA states before table used rather than inlining" );
            Console.Error.WriteLine( "  -Xnfastates            for nondeterminisms, list NFA states for each path" );
        }

        /// <summary>
        /// Set the location (base directory) where output files should be produced by the ANTLR tool.
        /// </summary>
        /// <param name="outputDirectory"></param>
        public virtual void setOutputDirectory( string outputDirectory )
        {
            haveOutputDir = true;
            this.outputDirectory = outputDirectory;
        }

        /**
         * Used by build tools to force the output files to always be
         * relative to the base output directory, even though the tool
         * had to set the output directory to an absolute path as it
         * cannot rely on the workign directory like command line invocation
         * can.
         *
         * @param forceRelativeOutput true if output files hould always be relative to base output directory
         */
        public virtual void setForceRelativeOutput( bool forceRelativeOutput )
        {
            this.forceRelativeOutput = forceRelativeOutput;
        }

        /**
         * Set the base location of input files. Normally (when the tool is
         * invoked from the command line), the inputDirectory is not set, but
         * for build tools such as Maven, we need to be able to locate the input
         * files relative to the base, as the working directory could be anywhere and
         * changing workig directories is not a valid concept for JVMs because of threading and
         * so on. Setting the directory just means that the getFileDirectory() method will
         * try to open files relative to this input directory.
         *
         * @param inputDirectory Input source base directory
         */
        public virtual void setInputDirectory( string inputDirectory )
        {
            this.inputDirectory = inputDirectory;
            haveInputDir = true;
        }

        public virtual TextWriter getOutputFile( Grammar g, string fileName )
        {
            if ( OutputDirectory == null )
                return new StringWriter();

            // output directory is a function of where the grammar file lives
            // for subdir/T.g, you get subdir here.  Well, depends on -o etc...
            // But, if this is a .tokens file, then we force the output to
            // be the base output directory (or current directory if there is not a -o)
            //
            System.IO.DirectoryInfo outputDir;
            if ( fileName.EndsWith( CodeGenerator.VOCAB_FILE_EXTENSION ) )
            {
                if ( haveOutputDir )
                {
                    outputDir = new System.IO.DirectoryInfo( OutputDirectory );
                }
                else
                {
                    outputDir = new System.IO.DirectoryInfo( "." );
                }
            }
            else
            {
                outputDir = getOutputDirectory( g.FileName );
            }
            FileInfo outputFile = new FileInfo( System.IO.Path.Combine( outputDir.FullName, fileName ) );

            if ( !outputDir.Exists )
                outputDir.Create();

            if ( outputFile.Exists )
                outputFile.Delete();

            return new System.IO.StreamWriter( new System.IO.BufferedStream( outputFile.OpenWrite() ) );
        }

        /**
         * Return the location where ANTLR will generate output files for a given file. This is a
         * base directory and output files will be relative to here in some cases
         * such as when -o option is used and input files are given relative
         * to the input directory.
         *
         * @param fileNameWithPath path to input source
         * @return
         */
        public virtual System.IO.DirectoryInfo getOutputDirectory( string fileNameWithPath )
        {
            string outputDir = OutputDirectory;

            if ( fileNameWithPath.IndexOfAny( System.IO.Path.GetInvalidPathChars() ) >= 0 )
                return new System.IO.DirectoryInfo( outputDir );

            if ( !System.IO.Path.IsPathRooted( fileNameWithPath ) )
                fileNameWithPath = System.IO.Path.GetFullPath( fileNameWithPath );

            string fileDirectory;
            // Some files are given to us without a PATH but should should
            // still be written to the output directory in the relative path of
            // the output directory. The file directory is either the set of sub directories
            // or just or the relative path recorded for the parent grammar. This means
            // that when we write the tokens files, or the .java files for imported grammars
            // taht we will write them in the correct place.
            //
            if ( fileNameWithPath.IndexOfAny( new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } ) == -1 )
            {
                // No path is included in the file name, so make the file
                // directory the same as the parent grammar (which might sitll be just ""
                // but when it is not, we will write the file in the correct place.
                //
                fileDirectory = grammarOutputDirectory;
            }
            else
            {
                fileDirectory = fileNameWithPath.Substring( 0, fileNameWithPath.LastIndexOfAny( new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } ) );
            }

            if ( haveOutputDir )
            {
                // -o /tmp /var/lib/t.g => /tmp/T.java
                // -o subdir/output /usr/lib/t.g => subdir/output/T.java
                // -o . /usr/lib/t.g => ./T.java
                if ( ( fileDirectory != null && !forceRelativeOutput ) &&
                     ( System.IO.Path.IsPathRooted( fileDirectory ) ||
                        fileDirectory.StartsWith( "~" ) ) || // isAbsolute doesn't count this :(
                        ForceAllFilesToOutputDir )
                {
                    // somebody set the dir, it takes precendence; write new file there
                    outputDir = OutputDirectory;
                }
                else
                {
                    // -o /tmp subdir/t.g => /tmp/subdir/t.g
                    if ( fileDirectory != null )
                    {
                        outputDir = System.IO.Path.Combine( OutputDirectory, fileDirectory );
                    }
                    else
                    {
                        outputDir = OutputDirectory;
                    }
                }
            }
            else
            {
                // they didn't specify a -o dir so just write to location
                // where grammar is, absolute or relative, this will only happen
                // with command line invocation as build tools will always
                // supply an output directory.
                //
                outputDir = fileDirectory;
            }
            return new System.IO.DirectoryInfo( outputDir );
        }

        /**
         * Name a file from the -lib dir.  Imported grammars and .tokens files
         *
         * If we do not locate the file in the library directory, then we try
         * the location of the originating grammar.
         *
         * @param fileName input name we are looking for
         * @return Path to file that we think shuold be the import file
         *
         * @throws java.io.IOException
         */
        public virtual string getLibraryFile( string fileName )
        {
            // First, see if we can find the file in the library directory
            //
            string f = Path.Combine( LibraryDirectory, fileName );

            if ( File.Exists( f ) )
            {
                // Found in the library directory
                //
                return Path.GetFullPath( f );
            }

            // Need to assume it is in the same location as the input file. Note that
            // this is only relevant for external build tools and when the input grammar
            // was specified relative to the source directory (working directory if using
            // the command line.
            //
            return Path.Combine( parentGrammarDirectory, fileName );
        }

        /** Return the directory containing the grammar file for this grammar.
         *  normally this is a relative path from current directory.  People will
         *  often do "java org.antlr.Tool grammars/*.g3"  So the file will be
         *  "grammars/foo.g3" etc...  This method returns "grammars".
         *
         *  If we have been given a specific input directory as a base, then
         *  we must find the directory relative to this directory, unless the
         *  file name is given to us in absolute terms.
         */
        public virtual string getFileDirectory( string fileName )
        {
            string f;
            if ( haveInputDir && !( fileName.StartsWith( Path.DirectorySeparatorChar.ToString() ) || fileName.StartsWith( Path.AltDirectorySeparatorChar.ToString() ) ) )
            {
                f = Path.Combine( inputDirectory, fileName );
            }
            else
            {
                f = fileName;
            }

            // And ask .NET what the base directory of this location is
            //
            return Path.GetDirectoryName( f );
        }

        /** Return a File descriptor for vocab file.  Look in library or
         *  in -o output path.  antlr -o foo T.g U.g where U needs T.tokens
         *  won't work unless we look in foo too. If we do not find the
         *  file in the lib directory then must assume that the .tokens file
         *  is going to be generated as part of this build and we have defined
         *  .tokens files so that they ALWAYS are generated in the base output
         *  directory, which means the current directory for the command line tool if there
         *  was no output directory specified.
         */
        public virtual FileInfo getImportedVocabFile( string vocabName )
        {
            string path = System.IO.Path.Combine( LibraryDirectory, vocabName + CodeGenerator.VOCAB_FILE_EXTENSION );
            if ( System.IO.File.Exists( path ) )
                return new FileInfo( path );

            // We did not find the vocab file in the lib directory, so we need
            // to look for it in the output directory which is where .tokens
            // files are generated (in the base, not relative to the input
            // location.)
            //
            if ( haveOutputDir )
            {
                path = Path.Combine( OutputDirectory, vocabName + CodeGenerator.VOCAB_FILE_EXTENSION );
            }
            else
            {
                path = vocabName + CodeGenerator.VOCAB_FILE_EXTENSION;
            }
            return new FileInfo( path );
        }

        /** If the tool needs to panic/exit, how do we do that?
         */
        public virtual void panic()
        {
            throw new Exception( "ANTLR panic" );
        }

        /// <summary>
        /// Return a time stamp string accurate to sec: yyyy-mm-dd hh:mm:ss
        /// </summary>
        public static string getCurrentTimeStamp()
        {
            return DateTime.Now.ToString( "yyyy\\-MM\\-dd HH\\:mm\\:ss" );
        }

        /**
         * Provide the List of all grammar file names that the ANTLR tool will
         * process or has processed.
         *
         * @return the grammarFileNames
         */
        public virtual HashSet<string> GrammarFileNames
        {
            get
            {
                return grammarFileNames;
            }
            set
            {
                grammarFileNames = value;
            }
        }

        /**
         * Indicates whether ANTLR has gnerated or will generate a description of
         * all the NFAs in <a href="http://www.graphviz.org">Dot format</a>
         *
         * @return the generate_NFA_dot
         */
        public virtual bool Generate_NFA_dot
        {
            get
            {
                return generate_NFA_dot;
            }
            set
            {
                this.generate_NFA_dot = value;
            }
        }

        /**
         * Indicates whether ANTLR has generated or will generate a description of
         * all the NFAs in <a href="http://www.graphviz.org">Dot format</a>
         *
         * @return the generate_DFA_dot
         */
        public virtual bool Generate_DFA_dot
        {
            get
            {
                return generate_DFA_dot;
            }
            set
            {
                this.generate_DFA_dot = value;
            }
        }

        /**
         * Return the Path to the base output directory, where ANTLR
         * will generate all the output files for the current language target as
         * well as any ancillary files such as .tokens vocab files.
         * 
         * @return the output Directory
         */
        public virtual string OutputDirectory
        {
            get
            {
                return outputDirectory;
            }
        }

        /**
         * Return the Path to the directory in which ANTLR will search for ancillary
         * files such as .tokens vocab files and imported grammar files.
         *
         * @return the lib Directory
         */
        public virtual string LibraryDirectory
        {
            get
            {
                return libDirectory;
            }
            set
            {
                this.libDirectory = value;
            }
        }

        /**
         * Indicate if ANTLR has generated, or will generate a debug version of the
         * recognizer. Debug versions of a parser communicate with a debugger such
         * as that contained in ANTLRWorks and at start up will 'hang' waiting for
         * a connection on an IP port (49100 by default).
         *
         * @return the debug flag
         */
        public virtual bool Debug
        {
            get
            {
                return debug;
            }
            set
            {
                debug = value;
            }
        }

        /**
         * Indicate whether ANTLR has generated, or will generate a version of the
         * recognizer that prints trace messages on entry and exit of each rule.
         *
         * @return the trace flag
         */
        public virtual bool Trace
        {
            get
            {
                return trace;
            }
            set
            {
                trace = value;
            }
        }

        /**
         * Indicates whether ANTLR has generated or will generate a version of the
         * recognizer that gathers statistics about its execution, which it prints when
         * it terminates.
         *
         * @return the profile
         */
        public virtual bool Profile
        {
            get
            {
                return profile;
            }
            set
            {
                profile = value;
            }
        }

        /**
         * Indicates whether ANTLR has generated or will generate a report of various
         * elements of the grammar analysis, once it it has finished analyzing a grammar
         * file.
         *
         * @return the report flag
         */
        public virtual bool Report
        {
            get
            {
                return report;
            }
            set
            {
                report = value;
            }
        }

        /**
         * Indicates whether ANTLR has printed, or will print, a version of the input grammar
         * file(s) that is stripped of any action code embedded within.
         *
         * @return the printGrammar flag
         */
        public virtual bool PrintGrammar
        {
            get
            {
                return printGrammar;
            }
            set
            {
                printGrammar = value;
            }
        }

        /**
         * Indicates whether ANTLR has supplied, or will supply, a list of all the things
         * that the input grammar depends upon and all the things that will be generated
         * when that grammar is successfully analyzed.
         *
         * @return the depend flag
         */
        public virtual bool Depend
        {
            get
            {
                return depend;
            }
            set
            {
                depend = value;
            }
        }

        public virtual bool TestMode
        {
            get
            {
                return testMode;
            }
            set
            {
                testMode = value;
            }
        }

        /**
         * Indicates whether ANTLR will force all files to the output directory, even
         * if the input files have relative paths from the input directory.
         *
         * @return the forceAllFilesToOutputDir flag
         */
        public virtual bool ForceAllFilesToOutputDir
        {
            get
            {
                return forceAllFilesToOutputDir;
            }
            set
            {
                forceAllFilesToOutputDir = value;
            }
        }

        /**
         * Indicates whether ANTLR will be verbose when analyzing grammar files, such as
         * displaying the names of the files it is generating and similar information.
         *
         * @return the verbose flag
         */
        public virtual bool Verbose
        {
            get
            {
                return verbose;
            }
            set
            {
                verbose = value;
            }
        }

        /**
         * Gets or sets the current setting of the conversion timeout on DFA creation.
         *
         * @return DFA creation timeout value in milliseconds
         */
        public virtual TimeSpan ConversionTimeout
        {
            get
            {
                return DFA.MAX_TIME_PER_DFA_CREATION;
            }
            set
            {
                DFA.MAX_TIME_PER_DFA_CREATION = value;
            }
        }

        /**
         * Gets or sets the current setting of the message format descriptor.
         */
        public virtual string MessageFormat
        {
            get
            {
                return ErrorManager.getMessageFormat().ToString();
            }
            set
            {
                ErrorManager.setFormat( value );
            }
        }

        /**
         * Returns the number of errors that the analysis/processing threw up.
         * @return Error count
         */
        public virtual int NumErrors
        {
            get
            {
                return ErrorManager.getNumErrors();
            }
        }

        /**
         * Indicate whether the tool will analyze the dependencies of the provided grammar
         * file list and ensure that grammars with dependencies are built
         * after any of the other gramamrs in the list that they are dependent on. Setting
         * this option also has the side effect that any grammars that are includes for other
         * grammars in the list are excluded from individual analysis, which allows the caller
         * to invoke the tool via org.antlr.tool -make *.g and not worry about the inclusion
         * of grammars that are just includes for other grammars or what order the grammars
         * appear on the command line.
         *
         * This option was coded to make life easier for tool integration (such as Maven) but
         * may also be useful at the command line.
         *
         * @return true if the tool is currently configured to analyze and sort grammar files.
         */
        public virtual bool Make
        {
            get
            {
                return make;
            }
            set
            {
                make = value;
            }
        }

        public virtual void addGrammarFile( string grammarFileName )
        {
            grammarFileNames.Add( grammarFileName );
        }
    }
}