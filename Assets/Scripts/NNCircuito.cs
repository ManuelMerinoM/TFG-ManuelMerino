using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(DecisionRequester))]
public class NNCircuito : Agent
{
    [System.Serializable]
    public class RewardInfo
    {
        public float no_movement = -0.1f;
        public float mult_forward = 0.001f;
        public float mult_backward = -0.001f;
        public float mult_barrier = -0.8f;
        public float mult_car = -0.5f;
    }

    public float Movespeed = 30;
    public float Turnspeed = 100;
    public RewardInfo rwd = new RewardInfo();
    public bool doEpisodes = true;
    private Rigidbody rb = null;
    private Vector3 posicion_original;
    private Quaternion rotacion_original;
    private Bounds bnd;

    public override void Initialize()
    {
        rb = this.GetComponent<Rigidbody>();
        rb.drag = 1;
        rb.angularDrag = 5;
        rb.interpolation = RigidbodyInterpolation.Extrapolate;

        this.GetComponent<MeshCollider>().convex = true;
        this.GetComponent<DecisionRequester>().DecisionPeriod = 1;
        bnd = this.GetComponent<MeshRenderer>().bounds;

        posicion_original = new Vector3(this.transform.position.x, this.transform.position.y, this.transform.position.z);
        rotacion_original = new Quaternion(this.transform.rotation.x, this.transform.rotation.y, this.transform.rotation.z, this.transform.rotation.w);
    }
    public override void OnEpisodeBegin()
    {
        rb.velocity = Vector3.zero;
        this.transform.position = posicion_original;
        this.transform.rotation = rotacion_original;
    }
    public override void OnActionReceived(ActionBuffers actions)
    {


        if (isBocaArriba() == false)
            return;

        float mag = Mathf.Abs(rb.velocity.sqrMagnitude);

        switch (actions.DiscreteActions.Array[0])   //moverse
        {
            case 0:
                AddReward(rwd.no_movement);// no moverse
                break;
            case 1:
                rb.AddRelativeForce(Vector3.back * Movespeed * Time.deltaTime, ForceMode.VelocityChange); //atras
                AddReward(mag * rwd.mult_backward);
                break;
            case 2:
                rb.AddRelativeForce(Vector3.forward * Movespeed * Time.deltaTime, ForceMode.VelocityChange); //alante
                AddReward(mag * rwd.mult_forward);
                break;
        }

        switch (actions.DiscreteActions.Array[1])   //girar
        {
            case 0:
                break;//no girar
            case 1:
                this.transform.Rotate(Vector3.up, -Turnspeed * Time.deltaTime); //izquierda
                break;
            case 2:
                this.transform.Rotate(Vector3.up, Turnspeed * Time.deltaTime); //derecha
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
        float mag = collision.relativeVelocity.sqrMagnitude;

        if (collision.gameObject.CompareTag("ParedExt") == true
            || collision.gameObject.CompareTag("ParedInt") == true)
        {
            AddReward(mag * rwd.mult_barrier);
            if (doEpisodes == true)
                EndEpisode();
        }
        else if (collision.gameObject.CompareTag("Coche") == true)
        {
            AddReward(mag * rwd.mult_car);
            if (doEpisodes == true)
                EndEpisode();
        }
    }
    private bool isBocaArriba()
    {
        //raycast down from car = ground should be closely there
        return Physics.Raycast(this.transform.position, -this.transform.up, bnd.size.y * 0.55f);
    }
}