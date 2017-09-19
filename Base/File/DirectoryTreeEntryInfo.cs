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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MosaicLib.Modular.Part;
using MosaicLib.Utils;

namespace MosaicLib.File
{
    #region DirectoryTreeEntryNode

    /// <summary>
    /// Class used to store information about a directory entry in a tree structure.  
    /// This class is used to contain information about a directory entry and includes the name, boolean properties to indicate if the entry is a file or a directory, 
    /// as well as information about the size of the file (if it is one) and the creation, modification and last access times for the entry.
    /// </summary>
    public class DirectoryTreeEntryNode : DirectoryEntryInfo
	{
        /// <summary>Default constructor - generates an empty tree item</summary>
        public DirectoryTreeEntryNode() 
            : base()
        {
            RootDirEntry = this;
            RootDirectoryTreeEntryNodeDictionary = new Dictionary<string, DirectoryTreeEntryNode>();
        }

        /// <summary>Standard Tree Node constructor for a given path and parent entry</summary>
        public DirectoryTreeEntryNode(string entryPath, DirectoryTreeEntryNode parentDirEntry) 
            : this()
        {
            RootDirEntry = parentDirEntry.RootDirEntry;
            RootDirectoryTreeEntryNodeDictionary = RootDirEntry.RootDirectoryTreeEntryNodeDictionary;

            ParentDirEntry = parentDirEntry;

            SetPathAndGetInfo(entryPath, clearFirst: false);
        }

        /// <summary>Reference to the Root node of this overall Tree (will be this node if this node is the root node).</summary>
        public DirectoryTreeEntryNode RootDirEntry { get; private set; }

        /// <summary>Reference to the Root node tree dictionary of this overall Tree to which all nodes are added and removed.</summary>
        public Dictionary<string, DirectoryTreeEntryNode> RootDirectoryTreeEntryNodeDictionary { get; private set; }

        /// <summary>Reference to the Parent of this entry in the overall Tree, or null for the tree root node.</summary>
        public DirectoryTreeEntryNode ParentDirEntry { get; private set; }

        /// <summary>A List of the nodes that are under this node in the tree</summary>
        public DirectoryTreeEntryNode[] DirContentsNodeArray { get { return UpdateAndGetSortedDirContentsNodeArray(); } }

        /// <summary>Returns the count of the number of nodes that are under this node</summary>
        public int DirContentsNodeCount { get { return sortedDirContentsNodeSet.Count; } }

        private class NodeSetSortComparer : IComparer<DirectoryTreeEntryNode>
        {
            int IComparer<DirectoryTreeEntryNode>.Compare(DirectoryTreeEntryNode x, DirectoryTreeEntryNode y)
            {
                // if the ages are different, return the order inverse of the normal order comparison.  put the smallest value (oldest) at the end of the set.
                int ageCompare = DateTime.Compare(x.OldestUtcTime, y.OldestUtcTime);
                if (ageCompare != 0)
                    return ageCompare * -1;

                // else return the comparison result for the relative Path string lexical ordering.
                int pathCompare = String.Compare(x.Path, y.Path);
                return -1 * pathCompare;
            }
        }
        private static readonly IComparer<DirectoryTreeEntryNode> nodeSetSortComparer = new NodeSetSortComparer();

        private SortedSet<DirectoryTreeEntryNode> sortedDirContentsNodeSet = new SortedSet<DirectoryTreeEntryNode>(nodeSetSortComparer);
        private DirectoryTreeEntryNode[] sortedDirContentsNodeArray = null;
        private DirectoryTreeEntryNode[] UpdateAndGetSortedDirContentsNodeArray() { return sortedDirContentsNodeArray ?? (sortedDirContentsNodeArray = sortedDirContentsNodeSet.ToArray()); }

        /// <summary>Index of the oldest sub-node in the DirContentsNodeArray or null if this Node's DirContentsNodeArray is null or empty.</summary>
        public DirectoryTreeEntryNode OldestDirContentsNodeEntry { get { return sortedDirContentsNodeSet.LastOrDefault(); } }       // linq is reasonably efficient for this case...

        /// <summary>Gives the total size of this node and all of the sub-nodes under this node</summary>
        public Int64 TreeContentsSize { get; private set; }

        /// <summary>Gives the oldest file creation time or directory modification time for this entry or any of its subs</summary>
        public DateTime OldestUtcTime 
        { 
            get 
            {
                DirectoryTreeEntryNode oldestEntry = OldestDirContentsNodeEntry;

                // update OldestUtcTime as the oldest UtcTime from the oldest entry or as the OldestFileSystemInfoDateTimeUTC if there is no oldest entry.
                return (oldestEntry != null) ? oldestEntry.OldestUtcTime : OldestFileSystemInfoDateTimeUtc;
            } 
        }

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

            sortedDirContentsNodeSet.Clear();
            sortedDirContentsNodeArray = null;
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
        public void RebuildTree(Logging.IMesgEmitter issueEmitter = null)
        {
            BuildTree(updateAtEnd: true, issueEmitter: issueEmitter, forceBuildTree: true);
        }

        /// <summary>Requests the node to build its sub-tree and to update it afterward</summary>
        public void BuildTree(Logging.IMesgEmitter issueEmitter) 
        {
            BuildTree(updateAtEnd: true, issueEmitter: issueEmitter); 
        }

        /// <summary>Requests the node to build its sub-tree and allows the caller to indicate if it should be updated after being built.</summary>
        public void BuildTree(bool updateAtEnd = true, Logging.IMesgEmitter issueEmitter = null, bool forceBuildTree = false)
        {
            try
            {
                // If the tree has already need built and does not have its IsTreeBuildNeeded flag set then do not (re)build this node
                if (!IsTreeBuildNeeded && !forceBuildTree)		// this flag should only be set for directories
                    return;

                issueEmitter = issueEmitter ?? Logging.NullEmitter;

                if (!IsDirectory)
                {
                    issueEmitter.Emit("BuildTree failed: Tree Path '{0}' is neither a normal file nor a normal directory.", Path);
                }

                ClearSubTreeInformation();

                sortedDirContentsNodeArray = null;
                System.IO.Directory.GetFileSystemEntries(Path).Select(path => new DirectoryTreeEntryNode(path, this)).DoForEach(node => sortedDirContentsNodeSet.Add(node));

                SetTreeUpdateNeeded(true);

                foreach (var entry in UpdateAndGetSortedDirContentsNodeArray())
                {
                    if (entry.IsExistingDirectory)
                        entry.BuildTree(issueEmitter: issueEmitter);
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
        }

        /// <summary>Updates the tree by recalculating the cumulative node information for this node and the nodes above as needed.</summary>
        public void UpdateTree(Logging.IMesgEmitter issueEmitter) 
        {
            UpdateTree(forceUpdate: false, issueEmitter: issueEmitter); 
        }

        /// <summary>Updates the tree by recalculating the cumulative node information for this node and the nodes above as needed.</summary>
        /// <param name="forceUpdate">Set this to true to force that this node gets recalculated even if its IsTreeUpdateNeeded flag has not already been set.</param>
        /// <param name="issueEmitter">provides the IMesgEmitter that will be used to report issues found while updating or rebuilding the tree.</param>
        public void UpdateTree(bool forceUpdate = false, Logging.IMesgEmitter issueEmitter = null)
        {
            issueEmitter = issueEmitter ?? Logging.NullEmitter;

            if (IsTreeBuildNeeded)
                BuildTree(updateAtEnd: false, issueEmitter: issueEmitter);

            if (!IsTreeUpdateNeeded && !forceUpdate)
                return;

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
        }

        /// <summary>Reports the Creation Age of the oldest item in the tree</summary>
        public TimeSpan TreeAge { get { return OldestUtcTime.Age(); } }

        /// <summary>
        /// Addes a new node in the tree at the relative location under the given node produced by iteratavely concactinating the given list of relative path elements
        /// onto the full path to this node and treversing downward until the relative path elements have been used up.  
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public void AddRelativePath(IList<String> relativePathElementList, Logging.IMesgEmitter issueEmitter = null)
        {
            issueEmitter = issueEmitter ?? Logging.NullEmitter;

            int relativePathElementListSize = relativePathElementList.Count;
            if (relativePathElementListSize <= 0)
            {
                issueEmitter.Emit("AddRelativePath failed at node:'{0}': given relative path list is empty", Path);
                return;
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
					else
					{
                        treeScanEntry.sortedDirContentsNodeArray = null;
                        treeScanEntry.sortedDirContentsNodeSet.Add(newEntry);

                        if (newEntry.IsExistingDirectory)
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

            UpdateTree(issueEmitter: issueEmitter);
        }

        /// <summary>
        /// Appends a list of the DirectoryEntryInfo items for the tree item's that have been removed from the memory copy of the tree.  
        /// Caller is expected to attempt to delete the actual files/directories.
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public void AppendAndRemoveOldestTreeItem(List<DirectoryEntryInfo> pruneItemList, Logging.IMesgEmitter issueEmitter = null)
        {
            AppendAndRemoveOldestTreeDirectory(pruneItemList, 1, issueEmitter);
        }

        /// <summary>
        /// Appends a list of the DirectoryEntryInfo items for the tree item's that have been removed from the memory copy of the tree.  
        /// Caller is expected to attempt to delete the actual files/directories.
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public void AppendAndRemoveOldestTreeDirectory(List<DirectoryEntryInfo> pruneItemList, int maxEntriesToDeletePerIteration, Logging.IMesgEmitter issueEmitter = null)
        {
            issueEmitter = issueEmitter ?? Logging.NullEmitter;

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
                    // 
					// we can never remove the root item - there is nothing further to prune.
					return;
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
                bool isNormalFile = topStackNode.IsExistingFile;
                bool isNormalDirectory = topStackNode.IsExistingDirectory;
                bool isEmptyDirectory = topStackNode.IsEmptyDirectory;

                string skipReason = null;
                if (!isNormalFile && !isNormalDirectory)
                    skipReason = Fcns.CheckedFormat("Skipping non-normal node at path:'{0}'", Path);

                bool skipIt = (skipReason != null);

                if (skipIt)
                    issueEmitter.Emit(skipReason);

                bool removeIt = (isNormalFile || isEmptyDirectory) && !skipIt;
                bool removeItFromTree = removeIt;

                if (!removeIt && !skipIt)
                    break;						// once we have reached a level where nothing more can be removed (ie it is not an empty directory), we stop iterating up the stack.

                if (removeItFromTree)
                {
                    if (removeIt)
                        pruneItemList.Add(topStackNode);		// append a copy of the topStackNode as a DirectoryEntryInfo object onto the pruneItemList

                    topStackNodeParentItem.sortedDirContentsNodeArray = null;

                    topStackNodeParentItem.sortedDirContentsNodeSet.Remove(topStackNode);
                    RootDirectoryTreeEntryNodeDictionary.Remove(topStackNode.Path);

                    if (removeIt && isFile && maxEntriesToDeletePerIteration > 1)
                    {
                        // identify the other n oldest items in this directory and add them to the delete list
                        while (pruneItemList.Count < maxEntriesToDeletePerIteration)
                        {
                            DirectoryTreeEntryNode nextOldestEntryInCurrentDir = topStackNodeParentItem.OldestDirContentsNodeEntry;

                            if (nextOldestEntryInCurrentDir == null)
                                break;

                            // stop adding to the current list of items to delete once we reach any non-normal file - special cases will be covered on the next go around.
                            if (!nextOldestEntryInCurrentDir.IsExistingFile)
                                break;

                            // append a copy of the nextEntryInCurrentDir as a DirectoryEntryInfo object
                            pruneItemList.Add(nextOldestEntryInCurrentDir);

                            // remove the pointer to the entry that just got added to the delete list and then delete the entry
                            topStackNodeParentItem.sortedDirContentsNodeSet.Remove(nextOldestEntryInCurrentDir);
                            RootDirectoryTreeEntryNodeDictionary.Remove(nextOldestEntryInCurrentDir.Path);

                            if (topStackNodeParentItem.IsEmptyDirectory)
                                break;
                        }
                    }

                    topStackNodeParentItem.SetTreeUpdateNeeded(true);
                }
            }

			UpdateTree(issueEmitter);
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
} // namespace MosaicLib.File
