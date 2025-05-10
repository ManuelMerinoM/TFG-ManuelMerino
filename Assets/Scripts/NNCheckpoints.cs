using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;


public class NNCheckpoints : Agent
{

    [SerializeField] private TrackCheckpoints trackCheckpoints;

    public float Movespeed = 30;
    public float Turnspeed = 100;
    public bool doEpisodes = true;
    private Rigidbody rb = null;
    private Vector3 posicion_original;
    private Quaternion rotacion_original;
    private Bounds bnd;
    void Start()
    {
        trackCheckpoints.OnCarCorrectCheckpoint += TrackCheckpoints_OnCarCorrecCheckpoint;
        trackCheckpoints.OnCarWrongCheckpoint += TrackCheckpoints_OnCarWrongCheckpoint;

        rb = this.GetComponent<Rigidbody>();
        bnd = this.GetComponent<MeshRenderer>().bounds;
        posicion_original = new Vector3(this.transform.position.x, this.transform.position.y, this.transform.position.z);
        rotacion_original = new Quaternion(this.transform.rotation.x, this.transform.rotation.y, this.transform.rotation.z, this.transform.rotation.w);
    }

    private void TrackCheckpoints_OnCarWrongCheckpoint(object sender, TrackCheckpoints.CarCheckPointEventArgs e)
    {
        if (e.carTransform == transform)
        {
            AddReward(-1f);
        }
    }

    private void TrackCheckpoints_OnCarCorrecCheckpoint(object sender, TrackCheckpoints.CarCheckPointEventArgs e)
    {
        if (e.carTransform == transform)
        {
            AddReward(1f);
        }
    }

    public override void OnEpisodeBegin()
    {
        rb.velocity = Vector3.zero;
        this.transform.position = posicion_original;
        this.transform.rotation = rotacion_original;

        trackCheckpoints.ResetCheckpoint(transform);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 checkpointForward = trackCheckpoints.GetNextCheckpointPosition(transform).transform.forward;
        float directionDot = Vector3.Dot(transform.forward, checkpointForward);
        sensor.AddObservation(directionDot);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        switch (actions.DiscreteActions.Array[0])   //moverse
        {
            case 0:
                break;
            case 1:
                rb.AddRelativeForce(Vector3.forward * Movespeed * Time.deltaTime, ForceMode.VelocityChange); //alante
                break;
            case 2:
                rb.AddRelativeForce(Vector3.back * Movespeed * Time.deltaTime, ForceMode.VelocityChange); //atras
                break;


        }

        switch (actions.DiscreteActions.Array[1])   //girar
        {
            case 0:
                break;//no girar
            case 1:
                this.transform.Rotate(Vector3.up, Turnspeed * Time.deltaTime); //derecha               
                break;
            case 2:
                this.transform.Rotate(Vector3.up, -Turnspeed * Time.deltaTime); //izquierda
                break;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {

        actionsOut.DiscreteActions.Array[0] = 0;
        actionsOut.DiscreteActions.Array[1] = 0;

        float move = Input.GetAxis("Vertical");
        float turn = Input.GetAxis("Horizontal");

        if (move < 0)
            actionsOut.DiscreteActions.Array[0] = 1;
        else if (move > 0)
            actionsOut.DiscreteActions.Array[0] = 2;

        if (turn < 0)
            actionsOut.DiscreteActions.Array[1] = 1;
        else if (turn > 0)
            actionsOut.DiscreteActions.Array[1] = 2;
    }

    private void OnCollisionEnter(Collision collision)
    {
        

        if (collision.gameObject.CompareTag("ParedExt") == true
            || collision.gameObject.CompareTag("ParedInt") == true)
        {
            AddReward(-0.5f);
            if (doEpisodes == true)
                EndEpisode();
        }
        else if (collision.gameObject.CompareTag("Coche") == true)
        {
            AddReward(-0.1f);
            if (doEpisodes == true)
                EndEpisode();
        }
    }

}

