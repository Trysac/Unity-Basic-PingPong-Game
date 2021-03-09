using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Ball : MonoBehaviour
{
    [SerializeField] Text scorePlayer1;
    [SerializeField] Text scorePlayer2;
    [SerializeField] float moveSpeed = 1f;
    [SerializeField] float speedAddedPerBounce = 1f;
    [SerializeField] float delayToStartGame = 2f;

    int socreP1 = 0;
    int scoreP2 = 0;

    float bounceCount = 0;
    
    bool gameOver = false;

    float xDireccion = 1;
    float yDirection = 1f;

    Rigidbody2D myRigidbody2D;

    void Start()
    {
        scorePlayer1.text = socreP1.ToString();
        scorePlayer2.text = scoreP2.ToString();
        myRigidbody2D = GetComponent<Rigidbody2D>();
        StartCoroutine(Delay());
    }
    private void Update()
    {

        if (scorePlayer1.text.Equals("5")) 
        {
            gameOver = true;
        }
        else if (scorePlayer2.text.Equals("5")) 
        {
            gameOver = true;
        }

        if (gameOver) 
        {
            SceneManager.LoadScene(2);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag.Equals("LP1")) 
        {
            scoreP2++;
            scorePlayer2.text = scoreP2.ToString();
            StartCoroutine(Delay());
        }
        else if (collision.gameObject.tag.Equals("LP2")) 
        {
            socreP1++;
            scorePlayer1.text = socreP1.ToString();
            StartCoroutine(Delay());
        }
        else if (collision.gameObject.tag.Equals("Pallet"))
        {         
            bounceCount++;
            PalletBounce(collision);
        }
    }

    private void PalletBounce(Collision2D collision) 
    {
        Vector2 ballPosition = transform.position;
        Vector2 palletPosition = collision.gameObject.transform.position;
        float palletHight = collision.collider.bounds.size.y;
        if (collision.gameObject.name.Equals("Pallet_Player_1")) 
        {
            xDireccion = 1f;
        }
        else 
        {
            xDireccion = -1f;
        }
        yDirection = (ballPosition.y - palletPosition.y) / palletHight;

        Vector2 newDirection = new Vector2(xDireccion, yDirection);
        MoveBall(newDirection);

    }

    private void MoveBall(Vector2 direction) 
    {
        Vector2 velocity = direction.normalized * ((moveSpeed + bounceCount * speedAddedPerBounce) * Time.deltaTime);
        myRigidbody2D.velocity = velocity;
    }

    private IEnumerator Delay()
    {
        transform.position = new Vector2(-1, 0);
        bounceCount = 0;
        myRigidbody2D.velocity = new Vector2(0, 0);
       yield return new WaitForSeconds(delayToStartGame);
        Vector2 inicialDirection = new Vector2(1,0);
        MoveBall(inicialDirection);
    } 
}
