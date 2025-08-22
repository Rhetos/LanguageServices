Param([ValidateSet('','remote','local')][string]$deleteFrom)
CHCP 65001

####################################
$mergedInBranch = 'master'
$keepBranches = 'master|rhetos-5' # Regular expression pattern.
####################################

git fetch --prune
$cleanBranchNames = ' -notmatch ''HEAD ->'' -replace ''^\*?\s*(origin/)?'' -notmatch ''^(' + $keepBranches + ')$'''
$remoteMergedBranches = Invoke-Expression "(git branch -r --merged origin/$mergedInBranch) $cleanBranchNames"
$localMergedBranches = Invoke-Expression "(git branch --merged origin/$mergedInBranch) $cleanBranchNames"

If ($deleteFrom -eq 'remote')
{
    $remoteMergedBranches | % { git push origin -d "refs/heads/$_"; TIMEOUT 2 }
}
ElseIf ($deleteFrom -eq 'local')
{
    $localMergedBranches | % { git branch -d $_ }
}
Else
{
"
REMOTE BRANCHES THAT CAN BE DELETED (MERGED TO $mergedInBranch):"
$remoteMergedBranches
"
LOCAL BRANCHES THAT CAN BE DELETED (MERGED TO $mergedInBranch):"
$localMergedBranches
"
INSTRUCTIONS:
- To delete the remote branches on the origin server (GitHub), run: ./gitclear.ps1 remote
- To delete the local branches on your PC, run: ./gitclear.ps1 local
"
}
