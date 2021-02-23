using System;
using System.Collections;
using System.Collections.Generic;
using DarkRift;
using DarkRift.Client;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private GameObject loginWindow;
    [SerializeField] private InputField nameInput;
    [SerializeField] private Button submitLoginButton;

    void Start()
    {
        submitLoginButton.onClick.AddListener(OnSubmitLogin);
        loginWindow.SetActive(false);
    }

    public void ShowLogin()
    {
        loginWindow.SetActive(true);
    }

    public void HideLogin()
    {
        loginWindow.SetActive(false);
    }

    public void OnSubmitLogin()
    {
        Debug.Log("Login submitted.");
        
        if (!String.IsNullOrEmpty(nameInput.text))
        {
            loginWindow.SetActive(false);
            
            using (Message message = Message.Create((ushort)NetworkingData.Tags.LoginRequest, new NetworkingData.LoginRequestData(nameInput.text)))
            {
                ConnectionManager.Instance.Client.SendMessage(message, SendMode.Reliable);
                Debug.Log("Login message sent.");
            }
        }
    }
    
    public IEnumerator LoadLogin()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Login");

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        
        Debug.Log("login loaded");
    }
}
