using UnityEngine; // Bounds, Vector3 and Mathf

public static class SiteClip
{
    // True if any part of the tile is within the site radius, measured on the
    // horizontal plane only so valleys and tall structures aren't clipped away.
    public static bool Intersects(Bounds tile, Vector3 siteCentre, float radius)
    {
        if (radius <= 0f) return true; // 0 disables clipping

        float x = Mathf.Clamp(siteCentre.x, tile.min.x, tile.max.x);
        float z = Mathf.Clamp(siteCentre.z, tile.min.z, tile.max.z);
        float dx = x - siteCentre.x, dz = z - siteCentre.z;

        return dx * dx + dz * dz <= radius * radius;
    }
}
