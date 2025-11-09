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

    void Start()
    {
        if ((myTabs == null || myTabs.Length == 0) && !string.IsNullOrEmpty(myTab_Tag))
        {
            myTabs = GameObject.FindGameObjectsWithTag(myTab_Tag);
        }

        ShowPage(0);
    }

    public void OpenTab(GameObject tabToOpen)
    {
        if (tabToOpen == null) return;

        int index = GetTabIndex(tabToOpen);
        if (index == -1) return;

        int pageStart = (index / PageSize) * PageSize;
        int pageEnd = Mathf.Min(pageStart + PageSize, myTabs.Length);

        for (int i = pageStart; i < pageEnd; i++)
            if (myTabs[i] != null)
                myTabs[i].SetActive(false);

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
            if (nextIndex < myTabs.Length)
            {
                InvokeCloseButton(myTabs[currentOpenTabIndex]);
                InvokeOpenButton(myTabs[nextIndex]);
                currentOpenTabIndex = nextIndex;
            }
            return;
        }

        int maxPage = Mathf.CeilToInt((float)myTabs.Length / PageSize) - 1;
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
                InvokeCloseButton(myTabs[currentOpenTabIndex]);
                InvokeOpenButton(myTabs[prevIndex]);
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

    /// <summary>
    /// Call this when the whole inventory UI is closed.
    /// It hides every tab, then shows page 0 again.
    /// </summary>
    public void CloseInventory()
    {
        // If a tab is currently open, run its CloseCharacter button first
        if (currentOpenTabIndex != -1 && currentOpenTabIndex < myTabs.Length)
        {
            InvokeCloseButton(myTabs[currentOpenTabIndex]);
        }

        // Hide all tabs
        for (int i = 0; i < myTabs.Length; i++)
        {
            if (myTabs[i] != null)
                myTabs[i].SetActive(false);
        }

        // Show page 0 (tabs 0¨C5)
        ShowPage(0);

        // Reset state
        currentOpenTabIndex = -1;
        currentPage = 0;
    }

    private void ShowPage(int pageIndex)
    {
        currentPage = pageIndex;
        int start = pageIndex * PageSize;
        int end = Mathf.Min(start + PageSize, myTabs.Length);

        // disable all
        for (int i = 0; i < myTabs.Length; i++)
            if (myTabs[i] != null) myTabs[i].SetActive(false);

        // enable this page
        for (int i = start; i < end; i++)
            if (myTabs[i] != null) myTabs[i].SetActive(true);
    }

    private int GetTabIndex(GameObject tab)
    {
        for (int i = 0; i < myTabs.Length; i++)
            if (myTabs[i] == tab) return i;
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
}