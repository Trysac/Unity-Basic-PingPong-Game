using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public void LoadGame() 
    {
        SceneManager.LoadScene(1);
    }

    public void LoadMainMenu() 
    {
        SceneManager.LoadScene(0);
    }

    public void Quit() 
    {
        Application.Quit();
    }
}
