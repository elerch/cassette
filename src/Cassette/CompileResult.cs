using System.Collections.Generic;

namespace Cassette
{
    public class CompileResult
    {
        public CompileResult(string output, IEnumerable<string> importedFilePaths)
        {
            Output = output;
            ImportedFilePaths = importedFilePaths;
        }

        public void AddSourceMap(string map)
        {
            SourceMap = map;
        }

        /// <summary>
        /// Source map data, if available
        /// </summary>
        public string SourceMap { get; private set; }

        /// <summary>
        /// The compiled output.
        /// </summary>
        public string Output { get; private set; }

        /// <summary>
        /// The application relative paths of files imported by the compiled source.
        /// </summary>
        public IEnumerable<string> ImportedFilePaths { get; private set; }
    }
}