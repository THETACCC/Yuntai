using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TabManager : MonoBehaviour
{
    [Header("All tabs in order")]
    public GameObject[] myTabs;

    [Header("Optional: tag to auto-fill")]
    public string myTab_Tag;

    private const int PageSize = 6;
    private int currentPage = 0;
    private int currentOpenTabIndex = -1; // -1 = no tab open

    // Internal list of tabs that are currently unlocked
    private List<GameObject> unlockedTabs = new List<GameObject>();

    void Start()
    {
        if ((myTabs == null || myTabs.Length == 0) && !string.IsNullOrEmpty(myTab_Tag))
        {
            myTabs = GameObject.FindGameObjectsWithTag(myTab_Tag);
        }

        RefreshUnlockedTabs();
        ShowPage(0);
    }

    /// <summary>
    /// Refreshes which tabs are considered available based on their "isThisUnlocked" script.
    /// </summary>
    public void RefreshUnlockedTabs()
    {
        unlockedTabs.Clear();
        foreach (GameObject tab in myTabs)
        {
            if (tab == null) continue;

            // Try to find the unlock script
            var unlockScript = tab.GetComponent<isThisUnlocked>();
            if (unlockScript == null || unlockScript.isThisThingUnlocked)
            {
                // Either no unlock script or it's unlocked
                unlockedTabs.Add(tab);
            }
            else
            {
                // Locked ¡ú hide
                tab.SetActive(false);
            }
        }
    }

    public void OpenTab(GameObject tabToOpen)
    {
        if (tabToOpen == null) return;

        int index = GetTabIndex(tabToOpen);
        if (index == -1) return;

        int pageStart = (index / PageSize) * PageSize;
        int pageEnd = Mathf.Min(pageStart + PageSize, unlockedTabs.Count);

        for (int i = pageStart; i < pageEnd; i++)
            if (unlockedTabs[i] != null)
                unlockedTabs[i].SetActive(false);

        tabToOpen.SetActive(true);
        currentOpenTabIndex = index;
        currentPage = pageStart / PageSize;
    }

    public void CloseTab(GameObject tabToClose)
    {
        if (tabToClose == null) return;
        int index = GetTabIndex(tabToClose);
        if (index == -1) return;

        int pageStart = (index / PageSize) * PageSize;
        ShowPage(pageStart / PageSize);
        currentOpenTabIndex = -1;
    }

    public void NextPage()
    {
        if (currentOpenTabIndex != -1)
        {
            int nextIndex = currentOpenTabIndex + 1;
            if (nextIndex < unlockedTabs.Count)
            {
                InvokeCloseButton(unlockedTabs[currentOpenTabIndex]);
                InvokeOpenButton(unlockedTabs[nextIndex]);
                currentOpenTabIndex = nextIndex;
            }
            return;
        }

        int maxPage = Mathf.CeilToInt((float)unlockedTabs.Count / PageSize) - 1;
        if (currentPage < maxPage)
        {
            currentPage++;
            ShowPage(currentPage);
        }
    }

    public void PreviousPage()
    {
        if (currentOpenTabIndex != -1)
        {
            int prevIndex = currentOpenTabIndex - 1;
            if (prevIndex >= 0)
            {
                InvokeCloseButton(unlockedTabs[currentOpenTabIndex]);
                InvokeOpenButton(unlockedTabs[prevIndex]);
                currentOpenTabIndex = prevIndex;
            }
            return;
        }

        if (currentPage > 0)
        {
            currentPage--;
            ShowPage(currentPage);
        }
    }

    public void CloseInventory()
    {
        if (currentOpenTabIndex != -1 && currentOpenTabIndex < unlockedTabs.Count)
        {
            InvokeCloseButton(unlockedTabs[currentOpenTabIndex]);
        }

        for (int i = 0; i < unlockedTabs.Count; i++)
        {
            if (unlockedTabs[i] != null)
                unlockedTabs[i].SetActive(false);
        }

        ShowPage(0);
        currentOpenTabIndex = -1;
        currentPage = 0;
    }

    private void ShowPage(int pageIndex)
    {
        RefreshUnlockedTabs(); // always recheck in case unlocks changed

        currentPage = pageIndex;
        int start = pageIndex * PageSize;
        int end = Mathf.Min(start + PageSize, unlockedTabs.Count);

        // disable all
        for (int i = 0; i < unlockedTabs.Count; i++)
            if (unlockedTabs[i] != null) unlockedTabs[i].SetActive(false);

        // enable this page
        for (int i = start; i < end; i++)
            if (unlockedTabs[i] != null) unlockedTabs[i].SetActive(true);
    }

    private int GetTabIndex(GameObject tab)
    {
        for (int i = 0; i < unlockedTabs.Count; i++)
            if (unlockedTabs[i] == tab) return i;
        return -1;
    }

    private void InvokeOpenButton(GameObject tab)
    {
        if (tab == null) return;

        Transform openChild = tab.transform.Find("Btn_OpenCharacter");
        Button openBtn = null;

        if (openChild != null)
            openBtn = openChild.GetComponent<Button>();
        else if (tab.transform.childCount > 0)
            openBtn = tab.transform.GetChild(0).GetComponent<Button>();

        if (openBtn != null)
            openBtn.onClick.Invoke();
        else
            OpenTab(tab);
    }

    private void InvokeCloseButton(GameObject tab)
    {
        if (tab == null) return;

        Transform closeChild = tab.transform.Find("Btn_CloseCharacter");
        Button closeBtn = null;

        if (closeChild != null)
            closeBtn = closeChild.GetComponent<Button>();
        else if (tab.transform.childCount > 1)
            closeBtn = tab.transform.GetChild(1).GetComponent<Button>();

        if (closeBtn != null)
            closeBtn.onClick.Invoke();
    }

    public List<GameObject> GetUnlockedInfo()
    {
        return unlockedTabs;
    }

    public void WriteUnlockedInfo(List<GameObject> unlockedTabs)
    {
        this.unlockedTabs = unlockedTabs;
    }
}