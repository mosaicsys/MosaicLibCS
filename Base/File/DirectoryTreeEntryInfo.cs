//-------------------------------------------------------------------
/*! @file DirectoryTreeEntryNode.cs
 *  @brief (see descriptions below)
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2011 Mosaic Systems Inc.
 * All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MosaicLib.Utils;

namespace MosaicLib.File
{
    #region DirectoryTreeEntryNode

    /// <summary>
    /// Class used to store information about a directory entry in a tree structure.  
    /// It specificially supports aggregatting information about a directory's sub-items into summary information that is maintained at the directory level.
    /// This summary information includes the Oldest sub-item, and the sum of the number of bytes, items and files that are at or under this directory and its sub-directories.
    /// </summary>
    public class DirectoryTreeEntryNode : DirectoryEntryInfo
	{
        /// <summary>Default constructor - generates an empty tree item</summary>
        public DirectoryTreeEntryNode() 
            : base()
        {
            _ParentDirEntry = null;

            _RootDirEntry = this;
            RootDirectoryTreeEntryNodeDictionary = new Dictionary<string, DirectoryTreeEntryNode>();
        }

        /// <summary>Standard Tree Node constructor for a given path and parent entry</summary>
        public DirectoryTreeEntryNode(string entryPath, DirectoryTreeEntryNode parentDirEntry) 
            : this()
        {
            _ParentDirEntry = parentDirEntry;

            _RootDirEntry = parentDirEntry.RootDirEntry;
            RootDirectoryTreeEntryNodeDictionary = RootDirEntry.RootDirectoryTreeEntryNodeDictionary;

            SetPathAndGetInfo(entryPath, clearFirst: false);
        }

        /// <summary>Reference to the Parent of this entry in the overall Tree, or null for the tree root node.</summary>
        public DirectoryTreeEntryNode ParentDirEntry { get { return _ParentDirEntry; } }
        private readonly DirectoryTreeEntryNode _ParentDirEntry;

        /// <summary>Reference to the Root node of this overall Tree (will be this node if this node is the root node).</summary>
        public DirectoryTreeEntryNode RootDirEntry { get { return _RootDirEntry; } }
        private readonly DirectoryTreeEntryNode _RootDirEntry;

        /// <summary>Reference to the Root node tree dictionary of this overall Tree to which all nodes are added and removed.</summary>
        public Dictionary<string, DirectoryTreeEntryNode> RootDirectoryTreeEntryNodeDictionary { get; private set; }

        /// <summary>A List of the nodes that are under this node in the tree</summary>
        public DirectoryTreeEntryNode[] DirContentsNodeArray { get { return UpdateAndGetSortedDirContentsNodeArray(); } }

        /// <summary>Returns the count of the number of nodes that are under this node</summary>
        public int DirContentsNodeCount { get { return sortedDirContentsNodeSet.Count; } }

        /// <summary>
        /// This class is used as the sort comparer for the sortedDirContentsNodeSet SortedSet.  
        /// It first compares the OldestUtcTime for the given x and y DirectoryTreeEntryNode instances,
        /// and then if they are equal it returns the comparison of the pair of corresponding Path values as strings.
        /// </summary>
        private class NodeSetSortComparer : IComparer<DirectoryTreeEntryNode>
        {
            int IComparer<DirectoryTreeEntryNode>.Compare(DirectoryTreeEntryNode x, DirectoryTreeEntryNode y)
            {
                // if the ages are different, return the order inverse of the normal order comparison.  put the largest value (oldest) at the end of the set.
                int ageCompare = DateTime.Compare(x.OldestUtcTime, y.OldestUtcTime);
                if (ageCompare != 0)
                    return ageCompare;

                // else return the comparison result for the relative Path string lexical ordering.
                int pathCompare = String.Compare(x.Path, y.Path);
                return pathCompare;
            }
        }
        /// <summary>Gives the single (re-enterant) node set sort comparer instance that is used by all corresponding SortedSet instances.</summary>
        private static readonly IComparer<DirectoryTreeEntryNode> nodeSetSortComparer = new NodeSetSortComparer();

        /// <summary>This set contains the nodes that are directly under this current directory node.  This set is sorted by OldestUtcTime from newest to oldest.</summary>
        private SortedSet<DirectoryTreeEntryNode> sortedDirContentsNodeSet { get { return _sortedDirContentsNodeSet ?? (_sortedDirContentsNodeSet = new SortedSet<DirectoryTreeEntryNode>(nodeSetSortComparer)); } }
        private SortedSet<DirectoryTreeEntryNode> _sortedDirContentsNodeSet;

        private DirectoryTreeEntryNode[] UpdateAndGetSortedDirContentsNodeArray() { return sortedDirContentsNodeArray ?? (sortedDirContentsNodeArray = sortedDirContentsNodeSet.SafeToArray()); }
        private DirectoryTreeEntryNode[] sortedDirContentsNodeArray = null;

        /// <summary>returns the oldest sub-node in the DirContentsNodeArray or null if this Node's DirContentsNodeArray is null or empty.</summary>
        public DirectoryTreeEntryNode OldestDirContentsNodeEntry { get { return sortedDirContentsNodeSet.FirstOrDefault(); } }

        /// <summary>Gives the total size of this node and all of the sub-nodes under this node</summary>
        public Int64 TreeContentsSize { get; private set; }

        /// <summary>Gives the total number of items under and including this node.</summary>
        public int TreeItemCount { get; private set; }

        /// <summary>Gives the total number of file items under and including this node.</summary>
        public int TreeFileCount { get; private set; }

        /// <summary>Returns true if some activity has indicated that the tree needs to be updated at this level.  Set by actions that change the tree contents.</summary>
        public bool IsTreeUpdateNeeded { get; private set; }

        /// <summary>
        /// Returns true if the tree needs to be built or rebuilt at this level in the tree.  
        /// Building the tree is relatively expensive as it fully re-enumerates the file system contents at and under this sub-path 
        /// and it completely replaces all of the Nodes under this point 
        /// </summary>
        public bool IsTreeBuildNeeded { get; private set; }

        /// <summary>
        /// For a file this gives the current FileSystemInfo.SafeGetOldestDateTimeUtc(fallbackValue: DateTime.MaxValue) at the time the FileSystemInfo was explicitly refreshed/replaced by this entry.
        /// For a non-empty directory this gives the DateTime of the oldest sub node (OldestDirContentsNodeEntry) at the time that this node was last updated.
        /// For an empty directory this gives the last value obtained from its prior contents if it had been non-empty at some prior point, or the SafeGetOldestDateTimeUtc from this node's DirectoryInfo if it has always been empty.
        /// </summary>
        public DateTime OldestUtcTime { get; private set; }

        /// <summary>
        /// When this directory is, or has been, non-empty this field gives the last OldestUtcTime from the sub-nodes at the point RefreshOldestUtcTime was last called. 
        /// </summary>
        private DateTime ? oldestSubNodeUtcDateTime = null;

        /// <summary>
        /// This method is used to update the OldestUtcTime.
        /// First the new oldest time is obtained and then if that is different than the prior value,
        /// then it updates the OldestUtcTime to the new value, removing and re-adding it to the parent's sortedDirContentsNodeSet as appropriate.
        /// For file nodes the newOldestUtcTime is obtained from FileSystemInfo.SafeGetOldestDateTimeUtc(fallbackValue: DateTime.MaxValue).
        /// For non-empty directory nodes the newOldestUtcTime is obtained from OldestDirContentsNodeEntry.OldestUtcTime.
        /// For empty directory nodes the newOldestUtcTime is obtained from oldestSubNodeUtcDateTime if the directory had been non-empty previously,
        /// otherwise newOldestUtcTime is set obtained from DirectoryInfo.SafeGetOldestDateTimeUtc(fallbackValue: DateTime.MaxValue).
        /// </summary>
        protected void UpdateOldestUtcTimeHereAndInParents()
        {
            var newOldestUtcTime = OldestUtcTime;

            if (IsExistingFile)
            {
                newOldestUtcTime = FileSystemInfo.SafeGetOldestDateTimeUtc(fallbackValue: DateTime.MaxValue);
            }
            else if (IsExistingDirectory)
            {
                var oldestSubNode = OldestDirContentsNodeEntry;
                if (oldestSubNode != null)
                {
                    newOldestUtcTime = oldestSubNode.OldestUtcTime;
                    oldestSubNodeUtcDateTime = newOldestUtcTime;
                }
                else if (oldestSubNodeUtcDateTime != null)
                    newOldestUtcTime = oldestSubNodeUtcDateTime ?? DateTime.MaxValue;
                else
                    newOldestUtcTime = FileSystemInfo.SafeGetOldestDateTimeUtc(fallbackValue: DateTime.MaxValue);
            }

            if (OldestUtcTime != newOldestUtcTime)
            {
                IsTreeUpdateNeeded = true;  // the call to parentDirEntry.UpdateOldestUtcTimeHereAndInParents() will ripple this up the tree as needed.

                var parentDirEntry = ParentDirEntry;

                if (parentDirEntry != null)
                {
                    parentDirEntry.sortedDirContentsNodeArray = null;
                    parentDirEntry.sortedDirContentsNodeSet.Remove(this);
                    OldestUtcTime = newOldestUtcTime;
                    parentDirEntry.sortedDirContentsNodeSet.Add(this);

                    parentDirEntry.UpdateOldestUtcTimeHereAndInParents();
                }
                else
                {
                    OldestUtcTime = newOldestUtcTime;
                }
            }
        }

        /// <summary>Resets this node to its fully empty state.  Also clears base.</summary>
        protected new void Clear()
        {
            if (!Path.IsNullOrEmpty())
                RootDirectoryTreeEntryNodeDictionary.Remove(Path);

            ClearSubTreeInformation();

            base.Clear();
        }

        /// <summary>Resets this node to its empty tree state.  Does not change relationship to its parent (if any)</summary>
        protected void ClearSubTreeInformation()
        {
            IsTreeUpdateNeeded = false;
            IsTreeBuildNeeded = false;

            ReleaseVector();

            TreeContentsSize = 0;
            TreeItemCount = 0;
            TreeFileCount = 0;
        }

        /// <summary>Sets that this level in the tree needs to be updated.  Setting the update request from false to true ripples up the tree from this point to the root.</summary>
        public void SetTreeUpdateNeeded(bool value)
        {
            if (!IsTreeUpdateNeeded && value)
            {
                IsTreeUpdateNeeded = true;

                // when set set this flag as a given level, we also make certain to set all the flags in the chain of entries above this level up to the tree root.
                if (!IsRootNode)
                    ParentDirEntry.SetTreeUpdateNeeded(true);
            }
            else if (IsTreeUpdateNeeded && !value)
            {
                IsTreeUpdateNeeded = false;
            }
            // else there is nothing to change
        }

        /// <summary>Returns true if this is the root Node</summary>
        public bool IsRootNode { get { return (ParentDirEntry == null); } }

        /// <summary>Returns true if this is an empty directory</summary>
        public bool IsEmptyDirectory { get { return ((DirContentsNodeCount == 0) && IsDirectory); } }

        /// <summary>Recursively clears the DirContentsNodeList and removes all of the elements from the node dictionary</summary>
        public void ReleaseVector()
		{
            foreach (var node in UpdateAndGetSortedDirContentsNodeArray())
            {
                node.ReleaseVector();
                RootDirectoryTreeEntryNodeDictionary.Remove(node.Path);
            }

            sortedDirContentsNodeArray = null;

            if (sortedDirContentsNodeSet.Count > 0)
            {
                sortedDirContentsNodeSet.Clear();

                UpdateOldestUtcTimeHereAndInParents();
            }
        }

        /// <summary>Getter returns the full path to the current object.  Setter Clears and resets the object and calls SetPathAndGetInfo.  Normally the tree must be Built after this before the contents can be traversed.</summary>
        public override string Path
        {
            get { return base.Path; }
            set
            {
                SetPathAndGetInfo(value);
            }
        }

        /// <summary>Resets the node to represent the contents of the tree from the given path.  Allows the caller to specify if the node should be cleared first.</summary>
        public void SetPathAndGetInfo(string path, bool clearFirst = true)
        {
            if (clearFirst)
                Clear();

            if (base.path != path)
            {
                RootDirectoryTreeEntryNodeDictionary.Remove(base.Path);
                RootDirectoryTreeEntryNodeDictionary[path] = this;
                base.Path = path;
            }

            TreeItemCount = 1;
            TreeFileCount = (IsFile ? 1 : 0);
            TreeContentsSize = Length;

            if (IsDirectory)
            {
                IsTreeBuildNeeded = true;
                SetTreeUpdateNeeded(true);
            }
        }

        /// <summary>Forces the node to (re)build the sub-tree and to update it afterward</summary>
        public DirectoryTreeEntryNode RebuildTree(Logging.IMesgEmitter issueEmitter = null)
        {
            return BuildTree(updateAtEnd: true, issueEmitter: issueEmitter, forceBuildTree: true);
        }

        /// <summary>Requests the node to build its sub-tree and to update it afterward</summary>
        public DirectoryTreeEntryNode BuildTree(Logging.IMesgEmitter issueEmitter) 
        {
            return BuildTree(updateAtEnd: true, issueEmitter: issueEmitter); 
        }

        /// <summary>Requests the node to build its sub-tree and allows the caller to indicate if it should be updated after being built.</summary>
        public DirectoryTreeEntryNode BuildTree(bool updateAtEnd = true, Logging.IMesgEmitter issueEmitter = null, bool forceBuildTree = false)
        {
            try
            {
                // If the tree has already need built and does not have its IsTreeBuildNeeded flag set then do not (re)build this node
                if (!IsTreeBuildNeeded && !forceBuildTree)		// this flag should only be set for directories
                    return this;

                issueEmitter = issueEmitter ?? Logging.NullEmitter;

                if (!IsDirectory)
                {
                    issueEmitter.Emit("BuildTree failed: Tree Path '{0}' is neither a normal file nor a normal directory.", Path);
                }

                ClearSubTreeInformation();

                sortedDirContentsNodeArray = null;
                System.IO.Directory.GetFileSystemEntries(Path)
                    .Select(path => new DirectoryTreeEntryNode(path, this))
                    .DoForEach(node => sortedDirContentsNodeSet.Add(node));

                SetTreeUpdateNeeded(true);

                foreach (var entry in UpdateAndGetSortedDirContentsNodeArray())
                {
                    if (entry.IsExistingDirectory)
                        entry.BuildTree(updateAtEnd: false, issueEmitter: issueEmitter);    // this will be done manually at the end when desired.
                    else if (!entry.IsExistingFile)
                        issueEmitter.Emit("BuildTree issue: Sub Tree Path '{0}' is neither a normal file nor a normal directory.", entry.Path);
                }

                // we have completed the tree build for this level and the levels below it
                IsTreeBuildNeeded = false;

                // update the tree to update our summary fields - only scan the top level as the 
                if (updateAtEnd)
                    UpdateTree(issueEmitter: issueEmitter);
            }
            catch (System.Exception ex)
            {
                issueEmitter.Emit("BuiltTree failed at path:'{0}' error:{1}", path, ex);
            }

            return this;
        }

        /// <summary>
        /// Updates the tree by recalculating the cumulative node information for this node and the nodes below as needed.
        /// <para/>Supports call chaining.
        /// </summary>
        public DirectoryTreeEntryNode UpdateTree(Logging.IMesgEmitter issueEmitter) 
        {
            return UpdateTree(forceUpdate: false, issueEmitter: issueEmitter); 
        }

        /// <summary>
        /// Updates the tree by recalculating the cumulative node information for this node (based on it and the ones below it) and the nodes below as needed.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="forceUpdate">Set this to true to force that this node gets recalculated even if its IsTreeUpdateNeeded flag has not already been set.</param>
        /// <param name="issueEmitter">provides the IMesgEmitter that will be used to report issues found while updating or rebuilding the tree.</param>
        public DirectoryTreeEntryNode UpdateTree(bool forceUpdate = false, Logging.IMesgEmitter issueEmitter = null)
        {
            issueEmitter = issueEmitter ?? Logging.NullEmitter;

            if (IsTreeBuildNeeded)
                BuildTree(updateAtEnd: false, issueEmitter: issueEmitter);

            if (!IsTreeUpdateNeeded && !forceUpdate)
                return this;

            IsTreeUpdateNeeded = false;

            Refresh();

            TreeContentsSize = Length;          // this is only non-zero for IsFile objects.
            TreeItemCount = 1;
            TreeFileCount = (IsFile ? 1 : 0);

            foreach (var entry in UpdateAndGetSortedDirContentsNodeArray())
            {
                if (entry.IsTreeBuildNeeded)
                    entry.BuildTree(issueEmitter: issueEmitter);
                else if (entry.IsTreeUpdateNeeded)
                    entry.UpdateTree(issueEmitter: issueEmitter);

                TreeContentsSize += entry.TreeContentsSize;
                TreeItemCount += entry.TreeItemCount;
                TreeFileCount += entry.TreeFileCount;
            }

            return this;
        }

        /// <summary>Reports the Creation Age of the oldest item in the tree</summary>
        public TimeSpan TreeAge { get { return OldestUtcTime.Age(); } }

        /// <summary>
        /// Addes a new node in the tree at the relative location under the given node produced by iteratavely concactinating the given list of relative path elements
        /// onto the full path to this node and treversing downward until the relative path elements have been used up.  
        /// When the target node is an already existing file in the tree then this operation calls Refresh on it.
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public DirectoryTreeEntryNode AddRelativePath(IList<String> relativePathElementList, Logging.IMesgEmitter issueEmitter = null)
        {
            issueEmitter = issueEmitter ?? Logging.NullEmitter;

            int relativePathElementListSize = relativePathElementList.Count;
            if (relativePathElementListSize <= 0)
            {
                issueEmitter.Emit("AddRelativePath failed at node:'{0}': given relative path list is empty", Path);
                return this;
            }

			DirectoryTreeEntryNode treeScanEntry = this;

			for (int rPathElemIdx = 0; ((treeScanEntry != null) && (rPathElemIdx < relativePathElementListSize)); rPathElemIdx++)
			{
				bool onLastRPathElement = (rPathElemIdx == (relativePathElementListSize - 1));

				string leafName = relativePathElementList[rPathElemIdx];
                string newEntryPath = System.IO.Path.Combine(treeScanEntry.Path, leafName);

                DirectoryTreeEntryNode subLeafNode = RootDirectoryTreeEntryNodeDictionary.SafeTryGetValue(newEntryPath);

                if (subLeafNode != null && subLeafNode.Name != leafName)
                    subLeafNode = treeScanEntry.UpdateAndGetSortedDirContentsNodeArray().FirstOrDefault(item => item.Name == leafName);

				if (subLeafNode == null)
				{
                    DirectoryTreeEntryNode newEntry = new DirectoryTreeEntryNode(newEntryPath, treeScanEntry);
                    subLeafNode = newEntry;

                    if (newEntry == null)
                    {
                        issueEmitter.Emit("Allocation Failure while attempting to added entry path:{0}", newEntryPath);
                    }
                    else if (newEntry.IsExistingDirectory)
                    {
                        newEntry.BuildTree(issueEmitter: issueEmitter);
                    }
                    else if (newEntry.IsExistingFile)
                    {
                        // nothing more to do here
                    }
                    else
                    {
                        issueEmitter.Emit("Added entry path:{0} is neither a known file or directory object", newEntry.Path);
                    }
				}
				else
				{
                    if (subLeafNode.IsFile)
                        subLeafNode.Refresh();

                    if (!subLeafNode.IsExistingDirectory && !onLastRPathElement)
					{
                        issueEmitter.Emit("Add relative path traverse error: partial path is not a directory at {0}", subLeafNode.Path);

						break;		 // cannot continue to go lower from this point down
					}
				}

                treeScanEntry.SetTreeUpdateNeeded(true);

                treeScanEntry = subLeafNode;
			}

            return UpdateTree(issueEmitter: issueEmitter);
        }

        /// <summary>
        /// Obtains new FileSystemInfo for this node's current Path and then updates the node based on that result.
        /// </summary>
        public override void Refresh()
        {
            UpdateFileSystemInfo(Path.GetFileSystemInfoForPath(), updatePath: false, updateName: false, updateTimeStamp: true);
        }

        /// <summary>
        /// Replaces the contained FileSystemInfo instance from the given <paramref name="fileSystemInfoIn"/>.  
        /// <para/>if the <paramref name="updatePath"/> parameter is true then this method sets the Path property from the FullName in the given <paramref name="fileSystemInfoIn"/>
        /// <para/>if the <paramref name="updateName"/> parameter is true then this method sets the Name property from the Name in the given <paramref name="fileSystemInfoIn"/>
        /// <para/>if the <paramref name="updateTimeStamp"/> parameter is true then this method set the TimeStamp to Now.
        /// <para/>In all cases this method calls UpdateOldestUtcTimeHereAndInParents on this node to handle any change in the effective "age" of this node and on any rippled
        /// effects this change can have on the effective ages of the directory nodes above it.
        /// </summary>
        public override void UpdateFileSystemInfo(FileSystemInfo fileSystemInfoIn, bool updatePath = true, bool updateName = true, bool updateTimeStamp = true)
        {
            bool entryIsDirectory = IsDirectory;

            base.UpdateFileSystemInfo(fileSystemInfoIn, updatePath: updatePath, updateName: updateName, updateTimeStamp: updateTimeStamp);

            UpdateOldestUtcTimeHereAndInParents();

            if (IsDirectory && !entryIsDirectory)
                IsTreeBuildNeeded = true;
        }

        /// <summary>
        /// Finds and removes the oldest node under this tree.  
        /// This removed node, along with any directories that were made empty by its removal are appended to the given <paramref name="pruneItemList"/>.
        /// Caller is expected to attempt to delete the actual files/directories.
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public DirectoryTreeEntryNode AppendAndRemoveOldestTreeItem(List<DirectoryEntryInfo> pruneItemList, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter traceEmitter = null)
        {
            return AppendAndRemoveOldestTreeDirectory(pruneItemList, 1, issueEmitter, traceEmitter);
        }

        /// <summary>
        /// Finds and removes the oldest node under this tree along with up to maxEntriesToDeletePerIteration-1 additional oldest nodes from the directory this one was found within.
        /// This set of 1 or more removed nodes, along with any directories that were made empty by its removal are appended to the given <paramref name="pruneItemList"/>.
        /// Caller is expected to attempt to delete the actual files/directories.
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public DirectoryTreeEntryNode AppendAndRemoveOldestTreeDirectory(List<DirectoryEntryInfo> pruneItemList, int maxEntriesToDeletePerIteration, Logging.IMesgEmitter issueEmitter = null, Logging.IMesgEmitter traceEmitter = null)
        {
            issueEmitter = issueEmitter ?? Logging.NullEmitter;
            traceEmitter = traceEmitter ?? Logging.NullEmitter;

            Stack<DirectoryTreeEntryNode> nodeStack = new Stack<DirectoryTreeEntryNode>();

			DirectoryTreeEntryNode currentEntry = this;

			// create a stack of the entries from the root down to the oldest leaf (file or directory).  
			//	Each level in the stack retains the current entry at that level, the parent entry and the index in the parent entries content vector at which you will find the current entry
			while (currentEntry != null)
			{
                DirectoryTreeEntryNode nextEntryDown = currentEntry.OldestDirContentsNodeEntry;

                if (nextEntryDown != null)
				{
					nodeStack.Push(nextEntryDown);

					currentEntry = nextEntryDown;
				}
				else if (!currentEntry.IsRootNode)
				{
					break;	// reached the bottom of the search path
				}
				else
				{
					// we can never remove the root item - there is nothing further to prune.
					return this;
				}
			}

			// start at the bottom of the stack and determine if that entry can be removed (or not).  If so then:
			//	A) added it to the list of entries to remove
			//	B) remove it from its parrent's entry content vector
			//	and then repeate for the each level up in the stack until an entry is encountered that cannot be deleted (is not an empty directory after the sub item has been removed.)

            while (nodeStack.Count != 0)
            {
                DirectoryTreeEntryNode topStackNode = nodeStack.Pop();
                DirectoryTreeEntryNode topStackNodeParentItem = topStackNode.ParentDirEntry;

                bool isFile = topStackNode.IsFile;
                bool isDirectory = topStackNode.IsDirectory;
                bool isEmptyDirectory = topStackNode.IsEmptyDirectory;
                bool isExistingFileOrDirectory = topStackNode.Exists;

                bool removeItFromTree = (isFile || isEmptyDirectory);
                bool removeIt = removeItFromTree && isExistingFileOrDirectory;

                if (!removeItFromTree)
                    break;						// once we have reached a level where nothing more can be removed (ie it is not an empty directory), we stop iterating up the stack.

                {
                    if (removeIt)
                    {
                        pruneItemList.Add(topStackNode);		// append a copy of the topStackNode as a DirectoryEntryInfo object onto the pruneItemList
                        traceEmitter.Emit("{0}: Adding node to be pruned: {1}", Fcns.CurrentMethodName, topStackNode);
                    }
                    else
                    {
                        issueEmitter.Emit("{0}: Dropping non-standard node: {1}", Fcns.CurrentMethodName, topStackNode);
                    }

                    topStackNodeParentItem.sortedDirContentsNodeArray = null;

                    topStackNodeParentItem.sortedDirContentsNodeSet.Remove(topStackNode);
                    RootDirectoryTreeEntryNodeDictionary.Remove(topStackNode.Path);

                    if (removeIt && isFile && maxEntriesToDeletePerIteration > 1)
                    {
                        string breakReason = null;

                        // identify the other n oldest items in this directory and add them to the delete list
                        for (; ; )
                        {
                            if (pruneItemList.Count >= maxEntriesToDeletePerIteration)
                            {
                                breakReason = "Reached prune item count limit ({0} >= {1})".CheckedFormat(pruneItemList.Count, maxEntriesToDeletePerIteration);
                                break;
                            }

                            DirectoryTreeEntryNode nextOldestEntryInCurrentDir = topStackNodeParentItem.OldestDirContentsNodeEntry;

                            if (nextOldestEntryInCurrentDir == null)
                            {
                                breakReason = "There are no more nodes in the current directory: {0}".CheckedFormat(topStackNodeParentItem);
                                break;
                            }

                            // stop adding to the current list of items to delete once we reach any non-normal file - special cases will be covered on the next go around.
                            if (!nextOldestEntryInCurrentDir.IsExistingFile)
                            {
                                breakReason = "Next node to evaluate is not an existing file: {0}".CheckedFormat(nextOldestEntryInCurrentDir);
                                break;
                            }

                            // append a copy of the nextEntryInCurrentDir as a DirectoryEntryInfo object
                            pruneItemList.Add(nextOldestEntryInCurrentDir);

                            // remove the pointer to the entry that just got added to the delete list and then delete the entry
                            topStackNodeParentItem.sortedDirContentsNodeSet.Remove(nextOldestEntryInCurrentDir);
                            RootDirectoryTreeEntryNodeDictionary.Remove(nextOldestEntryInCurrentDir.Path);

                            if (topStackNodeParentItem.IsEmptyDirectory)
                            {
                                breakReason = "Parent of current node is an empty directory: {0}".CheckedFormat(topStackNodeParentItem);
                                break;
                            }
                        }

                        if (breakReason.IsNeitherNullNorEmpty())
                            traceEmitter.Emit("{0}: node loop exited: {1}", Fcns.CurrentMethodName, breakReason);
                    }

                    topStackNodeParentItem.UpdateOldestUtcTimeHereAndInParents();

                    topStackNodeParentItem.SetTreeUpdateNeeded(true);
                }
            }

			return UpdateTree(issueEmitter);
        }

        /// <summary>Debug and logging assistance method</summary>
        public override string ToString()
        {
            string fullPath = (FileSystemInfo != null ? FileSystemInfo.FullName : "");
            string typeStr = "";
            if (IsFile)
                typeStr = "File";
            else if (IsDirectory)
                typeStr = "Directory";
            else
                typeStr = "UnknownType";

            if (Exists)
                typeStr = typeStr + "+Exists";

            if (IsRootNode)
                typeStr = typeStr + "+IsRoot";

            return Fcns.CheckedFormat("'{0}' {1} Items:{2} Files:{3} Size:{4} [{5}]", Name, typeStr, TreeItemCount, TreeFileCount, TreeContentsSize, fullPath);
        }
	};
	
    #endregion
}
