using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;
using UnityEngine.Assertions;

public class Player : MonoBehaviour {
    
    public GameObject groundFollowerObj; // The ground following object
    public GameObject playerObj; // Player object - containing model etc
    public SplineContainer trackContainer; // An object that contains Unitys spline component

    [Tooltip("Player model origin offset")]
    public Vector3 modelOffset = Vector3.zero;

    [Tooltip("Max jump height")]
    public float maxJumpHeight = 0f;

    [ Tooltip("How high in the air the player is") ]
    [ Range(0, 1) ]
    public float currJumpheight = 0f;

    [ Range(0, 1) ]
    [ Tooltip("How far along the spline we are") ]
    public float currPosition = 0f;

    [ Tooltip("Turns on movement simulation") ]
    public bool simulateMovement = true;

    [ Tooltip("How quickly to accellerate") ]
    public float accellerationRate = 1.4f;

    [ Tooltip("Minimum amount of speed we can reach") ]
    public float minVelocity = 0.01f;

    [ Tooltip("Maximum amount of speed we can reach") ]
    public float maxVelocity = 0.1f;

    [ Tooltip("Which direction we are facing") ]
    public Vector2 direction = Vector2.right; // We'll be travelling right all the time

    [ Tooltip("How much friction to apply when going up hills") ]
    public float hillFrictionAmount = 2f;

    [ Tooltip("How much friction to apply when riding almost horizontally") ]
    public float subtleFrictionAmount = 0.125f;

    float m_currVelocity = 0.1f; // Start off with some initial velocity
    Spline m_spline; // The spline we'll simulate movement on
    float m_prevPosition; // Track of old position
    Quaternion m_playerRotation; // Track of players rotation

    Vector3 m_splineUp = Vector3.zero;
    Vector3 m_splineTangent = Vector3.zero;


    // Start is called before the first frame update
    void Start() {
        Assert.IsNotNull( trackContainer, "Player does not have a track to follow" );
        m_spline = trackContainer.Splines.First<Spline>();

        // Add velocity at start to get moving
        m_currVelocity = 0.1f;
    }

    // Update is called once per frame
    void Update() {
        Assert.IsNotNull( groundFollowerObj, "No GroundFollower attached to Player script" );
        Assert.IsNotNull( playerObj, "No Player object attached to Player script " );

        // Get tangent and normal vectors from current position on spline
        m_splineTangent = trackContainer.transform.TransformDirection( SplineUtility.EvaluateTangent( m_spline, currPosition ) );
        m_splineUp = trackContainer.transform.TransformDirection( (Vector3)SplineUtility.EvaluateUpVector( m_spline, currPosition ) );

        // Get the dot product between direction and splineup
        // Credit to Freya Holmer for understanding dot product:
        // https://www.youtube.com/watch?v=2PrSUK1VrKA
        //
        // Essentially, we're checking how far away from direction vector, 
        // and using it to add to our vector 
        var dotResult = Vector3.Dot( direction.normalized, m_splineUp.normalized );

        if ( simulateMovement ) {
            if ( dotResult > 0 )
                // Accellerating down hills
                m_currVelocity = Mathf.Lerp( m_currVelocity, maxVelocity, Mathf.Abs( dotResult * accellerationRate ) * Time.deltaTime );

            else if ( dotResult < 0 )
                // Hill Friction (Lerp towards the slowest speed)
                m_currVelocity = Mathf.Lerp( m_currVelocity, minVelocity, Mathf.Abs( dotResult * hillFrictionAmount ) * Time.deltaTime );

            // Add a subtle amount of friction if the board is almost horizontal
            if ( dotResult > -0.1f && dotResult < 0.1f )
            {
                var amt = Mathf.Lerp( m_currVelocity, minVelocity, subtleFrictionAmount * Time.deltaTime );
                m_currVelocity = amt;
            }

            // Cap the velocity
            m_currVelocity = Mathf.Clamp(m_currVelocity, minVelocity, maxVelocity);
            
            // Finally add the velocity to the current spline position
            currPosition += m_currVelocity * Time.deltaTime;
        }

        // Move ground follower to current position along spline
        Vector3 splineWorldPos = trackContainer.transform.TransformPoint( m_spline.EvaluatePosition( currPosition ) );
        groundFollowerObj.transform.SetPositionAndRotation( splineWorldPos, Quaternion.LookRotation( m_splineTangent ) );

        // Handle jump visuals - Looks janky, but Player angle should be driven by the trajectory of the jump instead
        currJumpheight = Mathf.Clamp(currJumpheight, 0, 1f);
        Quaternion flatRotation = Quaternion.LookRotation(Vector3.right);
        Quaternion groundRotation = Quaternion.LookRotation(m_splineTangent);
        m_playerRotation = Quaternion.Lerp(flatRotation, groundRotation, (1f - currJumpheight) );

        // Set player position and rotation
        playerObj.transform.SetLocalPositionAndRotation( groundFollowerObj.transform.position 
                                                         + transform.rotation * modelOffset //Probably terrible, but it works right now..
                                                         + Vector3.up * (maxJumpHeight * currJumpheight), // Move the player model upwards based on jump height

                                                         m_playerRotation );

        // Draw debug lines and stuff
        DrawDebug();
    }

    public void DrawDebug() {
        // Draw a simplified speedometer at the back of the character
        float velocityHeight = m_currVelocity / maxVelocity;
        var groundPos = groundFollowerObj.transform.position;
        Debug.DrawRay( groundPos + m_splineUp.normalized * 1f, -m_splineTangent.normalized * 1.5f, Color.black );
        Debug.DrawRay( groundPos + m_splineUp.normalized * velocityHeight, -m_splineTangent.normalized * 1f, Color.black );

        // Draw the current height of the velocity for 10 seconds
        Debug.DrawRay( groundPos, m_splineUp.normalized * velocityHeight, Color.black, 10f);

        // Draw Curve up/normal
        Debug.DrawRay( groundPos, m_splineUp.normalized * 4f, new Color( 0.1f, 0.5f, 0.1f, 1f ) );
        
        // Draw Curve tangent
        Debug.DrawRay( groundPos - m_splineTangent.normalized * 5f, m_splineTangent.normalized * 10f, Color.blue );
        Debug.DrawRay( groundPos, direction.normalized * 10f, Color.red );

        // Draw arrow to/circle at where players max height is
        DebugDrawCircle(groundPos + Vector3.up * maxJumpHeight, 0.25f, Color.red);
        DebugDrawArrow(groundPos, groundPos + Vector3.up * maxJumpHeight, Color.red, 0.2f, 2.2f);
    }

    public void DebugDrawCircle(Vector2 position, float radius, Color color, int segments = 18)
    {
        float twoPi = (float)Mathf.PI * 2f;

        float angleShift = (twoPi / (float)segments);
        float phi = 0;

        for (int i = 0; i < segments; ++i, phi += angleShift)
        {
            Vector2 p1 = new Vector2( Mathf.Cos(phi), Mathf.Sin(phi)) * radius;
            Vector2 p2 = new Vector2( Mathf.Cos(phi + angleShift), Mathf.Sin(phi + angleShift)) * radius;
            Debug.DrawLine(position + p1, position + p2, color);
        }
    }

    public void DebugDrawArrow(Vector2 startPos, Vector2 endPos, Color color, float arrowSize = 0.1f, float startMargin = 0f, float endMargin = 0f )
    {
        Vector2 d = (endPos - startPos);
        Vector2 mid = d * 0.5f;
        Vector2 left = Quaternion.Euler(0, 0, -135f) * d;
        Vector2 right = Quaternion.Euler(0, 0, 135f) * d;

        Vector2 msp = startPos + (d.normalized * startMargin);
        Vector2 mep = startPos + d - (d.normalized * endMargin);

        Debug.DrawLine(msp, mep, color);
        Debug.DrawLine(mep, mep + left.normalized * arrowSize, color);
        Debug.DrawLine(mep, mep + right.normalized * arrowSize, color);
    }
}

  
