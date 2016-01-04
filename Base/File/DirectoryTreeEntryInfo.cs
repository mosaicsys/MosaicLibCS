//-------------------------------------------------------------------
/*! @file DirectoryTreeEntryNode.cs
	@brief
	(see descriptions below)

	Copyright (c) Mosaic Systems Inc.  All rights reserved.
	Copyright (c) 2011 Mosaic Systems Inc.  All rights reserved.

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

	     http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
 */
//-------------------------------------------------------------------

namespace MosaicLib.File
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;

	using MosaicLib.Modular.Part;
    using MosaicLib.Utils;

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
            DirContentsNodeList = new List<DirectoryTreeEntryNode>();
            OldestDirEntryIdx = -1;
            OldestUtcTime = DateTime.MaxValue;
        }

        /// <summary>Standard Tree Node constructor for a given path and parent entry</summary>
        public DirectoryTreeEntryNode(string entryPath, DirectoryTreeEntryNode parentDirEntry) 
            : this()
        {
            ParentDirEntry = parentDirEntry;
            SetPathAndGetInfo(entryPath, false);
        }

        /// <summary>Reference to the Parent of this entry in the overall Tree, or null for the tree root node.</summary>
        public DirectoryTreeEntryNode ParentDirEntry { get; private set; }
        /// <summary>A List of the nodes that are under this node in the tree</summary>
        public List<DirectoryTreeEntryNode> DirContentsNodeList { get; private set; }

        /// <summary>Index of the oldest sub-node in the DirContentsInfoList or -1 if this Node is the only or is the oldest.</summary>
        public int OldestDirEntryIdx { get; private set; }
        /// <summary>Gives the total size of this node and all of the sub-nodes under this node</summary>
        public Int64 TreeContentsSize { get; private set; }
        /// <summary>Gives the oldest file creation time or directory modification time for this entry or any of its subs</summary>
        public DateTime OldestUtcTime { get; private set; }
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
            ClearSubTreeInformation();
            base.Clear();
        }

        /// <summary>Resets this node to its empty tree state.  Does not change relationship to its parent (if any)</summary>
        protected void ClearSubTreeInformation()
        {
            IsTreeUpdateNeeded = false;
            IsTreeBuildNeeded = false;

            DirContentsNodeList.Clear();
            OldestDirEntryIdx = -1;
            TreeContentsSize = 0;
            OldestUtcTime = DateTime.MaxValue;
            TreeItemCount = 0;
            TreeFileCount = 0;
        }

        /// <summary>Sets that this level in the tree needs to be updated.  Setting the update request from false to true ripples up the tree from this point to the root.</summary>
        public void SetTreeUpdateNeeded(bool value)
        {
            bool entryIsTreeUpdateNeeded = IsTreeUpdateNeeded;

            IsTreeUpdateNeeded = value;

            // when set set this flag as a given level, we also make certain to set all the flags in the chain of entries above this level up to the tree root.
            if (IsTreeUpdateNeeded && !entryIsTreeUpdateNeeded && !IsRootNode)
                ParentDirEntry.SetTreeUpdateNeeded(true);
        }

        /// <summary>Returns true if this is the root Node</summary>
        public bool IsRootNode { get { return (ParentDirEntry == null); } }

        /// <summary>Returns true if this is an empty directory</summary>
        public bool IsEmptyDirectory { get { return ((DirContentsNodeList.Count == 0) && IsDirectory); } }

        /// <summary>Clears the DirContentsNodeList</summary>
        public void ReleaseVector()
		{
            DirContentsNodeList.Clear();
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

        /// <summary>Resets this node to represent the contents of the tree from the given path</summary>
        public void SetPathAndGetInfo(string path) 
        { 
            SetPathAndGetInfo(path, true); 
        }

        /// <summary>Resets the node to represent the contents of the tree from the given path.  Allows the caller to specify if the node should be cleared first.</summary>
        public void SetPathAndGetInfo(string path, bool clearFirst)
        {
            if (clearFirst)
                Clear();

            base.Path = path;

            TreeItemCount = 1;
            TreeFileCount = (IsFile ? 1 : 0);
            TreeContentsSize = Length;
            OldestUtcTime = OldestFileSystemInfoDateTimeUtc;

            if (IsDirectory)
            {
                IsTreeBuildNeeded = true;
                SetTreeUpdateNeeded(true);
            }
        }

        /// <summary>Forces the node to (re)build the tree and to update it afterward</summary>
        public void RebuildTree(Logging.IMesgEmitter issueEmitter)
        {
            IsTreeBuildNeeded = true;
            BuildTree(true, issueEmitter);
        }

        /// <summary>Requests the node to build the tree and to update it afterward</summary>
        public void BuildTree(Logging.IMesgEmitter issueEmitter) 
        { 
            BuildTree(true, issueEmitter); 
        }

        /// <summary>Requests the node to build the tree and allows the caller to indicate if it should be updated after being built.</summary>
        public void BuildTree(bool updateAtEnd, Logging.IMesgEmitter issueEmitter)
        {
            try
            {
                // If the tree has already need built and does not have its IsTreeBuildNeeded flag set then do not (re)build this node
                if (!IsTreeBuildNeeded)		// this flag should only be set for directories
                    return;

                if (!IsDirectory)
                {
                    issueEmitter.Emit("BuildTree failed: Tree Path '{0}' is neither a normal file nor a normal directory.", Path);
                }

                ClearSubTreeInformation();

                foreach (string fsiPath in System.IO.Directory.GetFileSystemEntries(Path))
                {
                    DirContentsNodeList.Add(new DirectoryTreeEntryNode(fsiPath, this));
                }

                SetTreeUpdateNeeded(true);

                int size = DirContentsNodeList.Count;
                for (int idx = 0; idx < size; idx++)
                {
                    DirectoryTreeEntryNode entry = DirContentsNodeList[idx];
                    if (entry == null)
                        continue;

                    if (entry.IsExistingDirectory)
                        entry.BuildTree(issueEmitter);
                    else if (!entry.IsExistingFile)
                        issueEmitter.Emit("BuildTree issue: Sub Tree Path '{0}' is neither a normal file nor a normal directory.", entry.Path);
                }

                // we have completed the tree build for this level and the levels below it
                IsTreeBuildNeeded = false;

                // update the tree to update our summary fields - only scan the top level as the 
                if (updateAtEnd)
                    UpdateTree(issueEmitter);
            }
            catch (System.Exception ex)
            {
                issueEmitter.Emit("BuiltTree failed at path:'{0}' error:{1}", path, ex);
            }
        }

        /// <summary>Updates the tree by recalculating the cumulative node information for this node and the nodes above as needed.</summary>
        public void UpdateTree(Logging.IMesgEmitter issueEmitter) 
        { 
            UpdateTree(false, issueEmitter); 
        }

        /// <summary>Updates the tree by recalculating the cumulative node information for this node and the nodes above as needed.</summary>
        /// <param name="forceUpdate">Set this to true to force that this node gets recalculated even if its IsTreeUpdateNeeded flag has not already been set.</param>
        /// <param name="issueEmitter">provides the IMesgEmitter that will be used to report issues found while updating or rebuilding the tree.</param>
        public void UpdateTree(bool forceUpdate, Logging.IMesgEmitter issueEmitter)
        {
            if (IsTreeBuildNeeded)
                BuildTree(false, issueEmitter);

            if (!IsTreeUpdateNeeded && !forceUpdate)
                return;

            IsTreeUpdateNeeded = false;

            Refresh();

            int dirContentsInfoListSize = DirContentsNodeList.Count;

            OldestDirEntryIdx = -1;
            TreeContentsSize = Length;          // this is only non-zero for IsFile objects.
            TreeItemCount = 1;
            TreeFileCount = (IsFile ? 1 : 0);

            for (int idx = 0; idx < dirContentsInfoListSize; idx++)
            {
                DirectoryTreeEntryNode entry = DirContentsNodeList[idx];
                if (entry == null)
                    continue;

                if (entry.IsTreeBuildNeeded)
                    entry.BuildTree(issueEmitter);
                else if (entry.IsTreeUpdateNeeded)
                    entry.UpdateTree(issueEmitter);

                TreeContentsSize += entry.TreeContentsSize;
                TreeItemCount += entry.TreeItemCount;
                TreeFileCount += entry.TreeFileCount;

                if ((OldestDirEntryIdx < 0 || OldestUtcTime > entry.OldestUtcTime) && (entry.IsFile || entry.IsDirectory))
                {
                    OldestUtcTime = entry.OldestUtcTime;
                    OldestDirEntryIdx = idx;
                }
            }

            if (OldestDirEntryIdx < 0)		// if no old sub-entries were found then use this item's create time as the oldest time.
                OldestUtcTime = OldestFileSystemInfoDateTimeUtc;
        }

        /// <summary>Reports the Creation Age of the oldest item in the tree</summary>
        public TimeSpan TreeAge { get { return (DateTime.UtcNow - OldestUtcTime); } }

        /// <summary>
        /// Addes a new node in the tree at the relative location under the given node produced by iteratavely concactinating the given list of relative path elements
        /// onto the full path to this node and treversing downward until the relative path elements have been used up.  
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public void AddRelativePath(IList<String> relativePathElementList, Logging.IMesgEmitter issueEmitter)
        {
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

				int subLeafIdx = treeScanEntry.FindSubLeafName(leafName);

				if (subLeafIdx < 0)
				{
					subLeafIdx = (treeScanEntry.DirContentsNodeList.Count);
                    string newEntryPath = System.IO.Path.Combine(treeScanEntry.Path, leafName);

					DirectoryTreeEntryNode newEntry = new DirectoryTreeEntryNode(newEntryPath, treeScanEntry);

					treeScanEntry.DirContentsNodeList.Add(newEntry);

					if (newEntry == null)
					{
						issueEmitter.Emit("Allocation Failure while attempting to added entry path:{0}", newEntryPath);
					}
					else
					{
                        if (newEntry.IsExistingDirectory)
                        {
                            // generally should be a noop
                            newEntry.BuildTree(issueEmitter);
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

					treeScanEntry.SetTreeUpdateNeeded(true);
				}
				else
				{
					DirectoryTreeEntryNode foundEntry = treeScanEntry.DirContentsNodeList[subLeafIdx];

					if (foundEntry == null)
					{
                        issueEmitter.Emit("Null pointer encountered while traversing tree to add new entry for leaf:'{0}' under path:'{1}'", leafName, Path);

						break;		 // cannot continue to go lower from this point down
					}
					else
					{
						if (foundEntry.IsFile)
							foundEntry.Refresh();

						if (!foundEntry.IsExistingDirectory && !onLastRPathElement)
						{
                            issueEmitter.Emit("Add relative path traverse error: partial path is not a directory at {0}", foundEntry.Path);

							break;		 // cannot continue to go lower from this point down
						}
					}

					treeScanEntry.SetTreeUpdateNeeded(true);
				}

				treeScanEntry = treeScanEntry.DirContentsNodeList[subLeafIdx];
			}

			UpdateTree(issueEmitter);
        }

        /// <summary>
        /// Internal Stack Item class used while traversing down to the oldest node for removal/pruning.  
        /// Allows parent directories to be quickly identified and evaluated to determine if they have become empty and need to be
        /// removed/pruned as well.
        /// </summary>
        private class DirectoryTreeEntryInfoStackItem
		{
            /// <summary>The current node</summary>
            public DirectoryTreeEntryNode Node { get; set; }
            /// <summary>The parent of the current node</summary>
            public DirectoryTreeEntryNode Parent { get; set; }
            /// <summary>The index of the current node in the parent node's DirContentsNodeList or -1 for the root.</summary>
            public int ItemIdxInParentList { get; set; }

            /// <summary>Default constructor</summary>
            public DirectoryTreeEntryInfoStackItem() 
            { 
                ItemIdxInParentList = -1; 
            }

            /// <summary>Returns true if the Node, and parent are non-null and if the ItemIdxInParentList is a valid index in the parent's sub-node list</summary>
            public bool IsValid 
			{
                get
                {
                    return (Node != null
                            && Parent != null
                            && ItemIdxInParentList >= 0
                            && (ItemIdxInParentList <= Parent.DirContentsNodeList.Count)
                            );
                }
			}
		};

        /// <summary>
        /// Appends a list of the DirectoryEntryInfo items for the tree item's that have been removed from the memory copy of the tree.  
        /// Caller is expected to attempt to delete the actual files/directories.
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public void AppendAndRemoveOldestTreeItem(List<DirectoryEntryInfo> pruneItemList, Logging.IMesgEmitter issueEmitter)
        {
            AppendAndRemoveOldestTreeDirectory(pruneItemList, 1, issueEmitter);
        }

        /// <summary>
        /// Appends a list of the DirectoryEntryInfo items for the tree item's that have been removed from the memory copy of the tree.  
        /// Caller is expected to attempt to delete the actual files/directories.
        /// Performs UpdateTree prior to returning.
        /// </summary>
        public void AppendAndRemoveOldestTreeDirectory(List<DirectoryEntryInfo> pruneItemList, int maxEntriesToDeletePerIteration, Logging.IMesgEmitter issueEmitter)
        {
			List<DirectoryTreeEntryInfoStackItem> stack = new List<DirectoryTreeEntryInfoStackItem>();

			DirectoryTreeEntryNode currentEntry = this;

			// create a stack of the entries from the root down to the oldest leaf (file or directory).  
			//	Each level in the stack retains the current entry at that level, the parent entry and the index in the parent entries content vector at which you will find the current entry
			while (currentEntry != null)
			{
				if (currentEntry.OldestDirEntryIdx >= 0)
				{
					int indexOfNextEntryInCurrent = currentEntry.OldestDirEntryIdx;

					DirectoryTreeEntryNode nextEntryDown = currentEntry.DirContentsNodeList[indexOfNextEntryInCurrent];

					stack.Add(new DirectoryTreeEntryInfoStackItem() { Node = nextEntryDown, Parent = currentEntry, ItemIdxInParentList = indexOfNextEntryInCurrent });

					currentEntry = nextEntryDown;
				}
				else if (!currentEntry.IsRootNode)
				{
					break;	// reached the bottom of the search path
				}
				else
				{
					// we can never remove the root item - there is nothing further to prune.
					return;
				}
			}

			// start at the bottom of the stack and determine if that entry can be removed (or not).  If so then:
			//	A) added it to the list of entries to remove
			//	B) remove it from its parrent's entry content vector
			//	and then repeate for the each level up in the stack until an entry is encountered that cannot be deleted (is not an empty directory after the sub item has been removed.)

			while (stack.Count != 0)
			{
				DirectoryTreeEntryInfoStackItem stackBack = stack[stack.Count - 1];
				bool removeIt = false;
                bool skipIt = false;

				if (!stackBack.IsValid)
					break;

				{
					DirectoryTreeEntryNode stackBackCurrentItem = stackBack.Node;
					DirectoryTreeEntryNode stackBackParentItem = stackBack.Parent;

                    bool isFile = stackBackCurrentItem.IsFile;
					bool isNormalFile = stackBackCurrentItem.IsExistingFile;
					bool isNormalDirectory = stackBackCurrentItem.IsExistingDirectory;
					bool isEmptyDirectory = stackBackCurrentItem.IsEmptyDirectory;

                    string skipReason = null;
                    if (!isNormalFile && !isNormalDirectory)
                        skipReason = Fcns.CheckedFormat("Skipping non-normal node at path:'{0}'", Path);

                    skipIt = skipReason != null;

                    if (skipIt)
                        issueEmitter.Emit(skipReason);

                    removeIt = (isNormalFile || isEmptyDirectory) && !skipIt;

					if (!removeIt && !skipIt)
						break;						// once we have reached a level where nothing more can be removed (ie it is not an empty directory), we stop iterating up the stack.

                    if (removeIt)
    					pruneItemList.Add(stackBackCurrentItem);		// append a copy of the backEntry as a DirectoryEntryInfo object onto the removeItemVect

					stackBackParentItem.DirContentsNodeList.RemoveAt(stackBack.ItemIdxInParentList);

					stackBackParentItem.SetTreeUpdateNeeded(true);

					if (isFile && maxEntriesToDeletePerIteration > 1)
					{
						// identify the other n oldest items in this directory and add them to the delte list
						while (pruneItemList.Count < maxEntriesToDeletePerIteration)
						{
							stackBackParentItem.UpdateTree(issueEmitter);

							int indexOfNextEntryInCurrentDir = stackBackParentItem.OldestDirEntryIdx;
							if (indexOfNextEntryInCurrentDir < 0)
								break;

							DirectoryTreeEntryNode nextEntryInCurrentDir = stackBackParentItem.DirContentsNodeList[indexOfNextEntryInCurrentDir];

							if (nextEntryInCurrentDir == null)
								break;

                            // stop adding to the current list of items to delete once we reach any non-normal file - special cases will be covered on the next go around.
                            if (!nextEntryInCurrentDir.IsExistingFile)
								break;

							// append a copy of the nextEntryInCurrentDir as a DirectoryEntryInfo object
							pruneItemList.Add(nextEntryInCurrentDir);		

							// remove the pointer to the entry that just got added to the delete list and then delete the entry
							stackBackParentItem.DirContentsNodeList.RemoveAt(indexOfNextEntryInCurrentDir);

							stackBackParentItem.SetTreeUpdateNeeded(true);

							if (stackBackParentItem.IsEmptyDirectory)
								break;
						}
					}
				}

                stack.RemoveAt(stack.Count - 1);
			}

			UpdateTree(issueEmitter);
        }

        /// <summary>Returns the index of the subNode with the given Name from the list of nodes that are directly under this one.  Returns -1 if no such Name was found.</summary>
        private int FindSubLeafName(string subLeafName)
		{
			int dirContentsInfoListSize = DirContentsNodeList.Count;
			for (int idx = 0; idx < dirContentsInfoListSize; idx++)
			{
				DirectoryTreeEntryNode subEntry = DirContentsNodeList[idx];
				if (subEntry != null && (subEntry.Name == subLeafName))
					return idx;
			}

			return -1;
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
