<#
.SYNOPSIS
Checks the lists that open from a button rather than a list box, in pretend
mode: the profile list in the side menu and Home's chat apps, that they choose
as every list does, and pictures them open for a person to look over.

.DESCRIPTION
Run through run.ps1. Saves one profile from the side menu, so its list has a
row, then chooses two chat apps and clears them. Nothing here changes a real
headset.

.PARAMETER Parts
Which parts to run: A dark, B light. Both by default.
#>
param([string]$Parts = 'AB')

. "$PSScriptRoot\Ui.ps1"

# A row of a list open over the window, by the start of its name.
function Find-RowLike([string]$Like) {
    foreach ($window in $A::RootElement.FindAll($Scope::Children, (New-Condition ProcessIdProperty ([int]$script:Proc.Id)))) {
        $row = @($window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))) |
            Where-Object { $_.Current.Name -like $Like } | Select-Object -First 1
        if ($row) { return $row }
    }
}

function Get-Chosen($Row) { $Row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected }

function Find-ChatField {
    @(Search { $script:Window.FindAll($Scope::Descendants, (New-Condition ControlTypeProperty ([System.Windows.Automation.ControlType]::Button))) }) |
        Where-Object { $_.Current.Name -like 'Chat comes from*' } | Select-Object -First 1
}

foreach ($part in @(@('A', 'dark'), @('B', 'light'))) {
    if (-not $Parts.Contains($part[0])) { continue }
    $theme = $part[1]
    Start-NeapPretend -Theme $theme -Page home
    try {
        Set-NeapSize 1400 1100 'Profile'
        Wait-Until { $null -ne (Find-ChatField) } 20 | Out-Null

        Section "the profile list ($theme)"
        Open-Menu (Find-Named 'Profile')
        Check (Wait-Until { $null -ne (Find-Anywhere 'New profile') -and (Find-Anywhere 'New profile').Current.IsEnabled } 15) 'it offers New profile once the headset has answered'
        Press (Find-Anywhere 'New profile')
        Check (Wait-Until { $null -ne (Find-Anywhere 'Profile name' ([System.Windows.Automation.ControlType]::Edit)) } 5) 'it asks for a name'
        (Find-Anywhere 'Profile name' ([System.Windows.Automation.ControlType]::Edit)).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Game night')
        Wait-Until { (Find-Anywhere 'Save').Current.IsEnabled } 3 | Out-Null
        Press (Find-Anywhere 'Save')
        Open-Menu (Find-Named 'Profile')
        Check (Wait-Until { $null -ne (Find-RowLike 'Game night') } 5) 'the list has the profile saved'
        Check (Get-Chosen (Find-RowLike 'Game night')) 'chosen, as it is in use'
        Save-ListShot "lists-profile-$theme"
        # Pressed again to close, as the profile button opens a flyout rather than expanding.
        Press (Find-Named 'Profile')
        Check (Wait-Until { $null -eq (Find-RowLike 'Game night') } 4) 'and it closes'

        Section "the chat apps ($theme)"
        Press (Find-ChatField)
        Check (Wait-Until { $null -ne (Find-RowLike 'Pretend Chat*') } 5) 'the list offers the apps playing'
        $chat = Find-RowLike 'Pretend Chat*'
        $music = Find-RowLike 'Pretend Music*'
        $chat.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).AddToSelection()
        $music.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).AddToSelection()
        Check (Wait-Until { (Find-ChatField).Current.Name -eq 'Chat comes from Pretend Chat and Pretend Music' } 4) "two can be chosen: $((Find-ChatField).Current.Name)"
        Check ((Get-Chosen $chat) -and (Get-Chosen $music)) 'and both show as chosen'
        Save-ListShot "lists-chat-$theme"
        $chat.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).RemoveFromSelection()
        $music.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).RemoveFromSelection()
        Check (Wait-Until { (Find-ChatField).Current.Name -eq 'Chat comes from No app chosen yet' } 4) 'and both can be cleared'
    }
    finally { Stop-NeapPretend }
}
