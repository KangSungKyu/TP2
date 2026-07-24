using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class QuadraticBezierRenderer : MonoBehaviour
{
    [SerializeField]
    private bool isUpdatePerFrame = false;
    [SerializeField, Range(5, 50)]
    private int segmentCount = 10;
    [SerializeField]
    private Vector3 pointA = Vector3.zero;
    [SerializeField]
    private Vector3 controlPoint = Vector3.zero;
    [SerializeField]
    private Vector3 pointB = Vector3.zero;
    [SerializeField]
    private GameObject endPoint = null;


    private Color lineColor = Color.white;
    private LineRenderer lineRenderer = null;
    private Tweener doAnim = null;

    public void SetBezierPoints(Vector3 pA, Vector3 ctrlP, Vector3 pB)
    {
        pointA = pA;
        controlPoint = ctrlP;
        pointB = pB;
    }

    public void SetColor(Color color)
    {
        lineColor = color;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        endPoint.transform.GetChild(0).GetComponent<SpriteRenderer>().color = lineColor;
    }

    public void PlayColorAnim()
    {
        if(doAnim != null)
        {
            doAnim.Restart();
        }
        else
        {
            Color alphaColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.0f);
            Color2 startColor = new Color2(lineColor, lineColor);
            Color2 endColor = new Color2(alphaColor, alphaColor);

            doAnim = lineRenderer.DOColor(startColor, endColor, 1.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

            doAnim.Play();
        }
    }

    public void StopColorAnim()
    {
        doAnim?.Kill();
        doAnim = null;

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
    }

    public void Render()
    {
        lineRenderer.positionCount = segmentCount + 1;

        for(int i = 0; i <= segmentCount; i++) 
        {
            float t = (float)i / segmentCount;
            Vector3 pos = Util.CalcBezierPoint_Quadratic(t, pointA, controlPoint, pointB);

            lineRenderer.SetPosition(i, pos);
        }

        if (segmentCount > 0)
        {
            Vector3 end = lineRenderer.GetPosition(segmentCount);
            Vector3 prev = lineRenderer.GetPosition(segmentCount - 1);
            Vector3 dir = (end - prev).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion q = Quaternion.Euler(0, 0, angle);


            endPoint.transform.position = end;
            endPoint.transform.rotation = q;

            endPoint.SetActive(true);
        }
        else
        {
            endPoint.SetActive(false);
        }
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        if (isUpdatePerFrame)
        {
            Render();
        }
    }
}