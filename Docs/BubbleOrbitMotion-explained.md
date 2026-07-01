# How `BubbleOrbitMotion` works (beginner-friendly)

A plain-language walkthrough of `Assets/Thought Bubble/Scripts/BubbleOrbitMotion.cs`.
Almost none of this needs math beyond Calculus 1. The "scary" parts are really just
**vectors**, and the one genuinely new idea is the **cross product** (explained below
with no formulas).

---

## The big picture: a 3-state machine

The bubble is always in exactly one of three `Phase`s:

- **`Idle`** — sitting still, not thrown. The script does nothing.
- **`Flying`** — just thrown, traveling in a straight line.
- **`Orbiting`** — circling the user.

`FixedUpdate()` runs ~50×/sec (Unity's physics tick) and behaves differently
depending on the phase. The whole script is just "which phase am I in, and what do
I do this tick."

---

## Vector vocabulary (the only background you need)

A **vector** is just 3 numbers (x, y, z). It can mean a **position** (where something
is) or a **direction/velocity** (which way + how fast). Two operations show up constantly:

- **`.magnitude`** = the **length** of the vector. For a velocity, that's the *speed*.
  It's literally the 3D Pythagorean theorem: `sqrt(x² + y² + z²)`.
- **A "unit vector" / direction** = a vector divided by its own length, so its length
  becomes exactly 1. It throws away "how long" and keeps only "which way."
  Example (line 140): `radialDir = radial / r` — take the arrow, divide by its length
  `r`, get pure direction.

With those two ideas the orbit math reads cleanly.

---

## The dome shape = Pythagoras

This line (95 and 129) is the only geometry:

```csharp
float ring = Mathf.Sqrt(domeRadius * domeRadius - h * h);   // = sqrt(R² - h²)
```

Picture a dome (half-sphere) of radius `R` over the user. Slice it horizontally at
height `h`. That slice is a circle — what radius? A right triangle: the dome radius
`R` is the hypotenuse, the height `h` is one leg, the ring radius is the other leg.
Rearranged Pythagoras:

```
ring² + h² = R²   →   ring = sqrt(R² − h²)
```

So a bubble high up (big `h`) gets a small ring; a bubble at the user's level
(`h ≈ 0`) gets a ring near the full `R`. That's what makes the bubbles trace a dome
instead of a cylinder. `Mathf.Max(..., 0f)` just prevents a negative inside the
square root (which would be NaN) if a bubble is somehow higher than the dome.

---

## Phase 1 → 2: flying straight, then deciding to curve

Because the Rigidbody has no gravity, a thrown bubble keeps its velocity and goes
straight on its own — the script doesn't even push it during `Flying`. It only
**watches the distance** (line 125):

```csharp
if (Vector3.Distance(_launchPoint, pos) >= curveStartDistance)
```

`_launchPoint` was recorded when thrown; `pos` is now. Once it's flown far enough,
the script locks in the orbit for this bubble: it remembers the current height
(`_orbitY`), computes the ring radius from the dome equation, and switches to
`Orbiting`. Until then it just `return`s and lets it coast.

---

## Phase 3: the orbit steering (the heart of it)

Once orbiting, every tick the script computes a **desired velocity** — the velocity
that *would* keep it circling perfectly — and then nudges the real velocity toward
it. The desired velocity (lines 145–148) is the sum of **three "wishes":**

### Wish 1 — go around the ring (the tangent)

First it finds the **radial direction**: the arrow from the center straight out to
the bubble (lines 137–140), flattened to horizontal (`radial.y = 0`) and shrunk to
length 1. That points *outward*. But to circle, you don't move outward — you move
*sideways*, 90° from "outward." That sideways direction is the **tangent**.

To get it, line 142 uses the **cross product**:

```csharp
Vector3 tangentDir = Vector3.Cross(Vector3.up, radialDir);
```

The only thing you need to know about a cross product: **give it two directions, and
it hands back a new direction that's perpendicular to both.** "Up" crossed with
"straight outward" gives "horizontal sideways" — exactly the way you'd walk to go
around a circle.

> Tetherball analogy: the rope points outward (radial), up is vertical, and the ball
> travels sideways around the pole (tangent).

Line 143 flips it for clockwise vs counter-clockwise. Multiply by `orbitSpeed` and
you have "travel around the ring at this speed."

### Wish 2 — sit at the right radius

```csharp
+ radialDir * ((_targetRadius - r) * radialGain)
```

`r` is the bubble's current distance from center; `_targetRadius` is where it
*should* be. `(_targetRadius - r)` is the error: positive if it's too close (push
outward), negative if too far (pull inward). It's a **spring** — the farther off, the
harder the correction. This is what makes it spiral gently onto the ring instead of
needing a perfect throw.

### Wish 3 — hold its height

```csharp
+ Vector3.up * ((_orbitY - pos.y) * verticalGain)
```

Same spring idea, vertically: if it has drifted below its locked-in height, push up;
if above, push down. This keeps each bubble at a constant Y (a flat circle).

### The easing — why it "eases" in instead of snapping

Line 150:

```csharp
_rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, desiredVel, settleSharpness * Time.fixedDeltaTime);
```

`Lerp(a, b, t)` = "move fraction `t` of the way from `a` to `b`." With a small `t`
each tick, the actual velocity *chases* the desired velocity a little bit every frame
rather than jumping to it. That's what turns the throw into a smooth banking curve
into orbit. Bigger `settleSharpness` = snappier; smaller = lazier, wider curve.
(`Time.fixedDeltaTime` is the time per physics tick, which keeps the easing speed
consistent regardless of framerate.)

---

## How the pieces connect each tick (Orbiting)

```
where am I relative to center?      → radial direction + distance r
which way is "around"?              → cross(up, radial) = tangent      (Wish 1)
am I at the right distance?         → spring on (targetRadius − r)     (Wish 2)
am I at the right height?           → spring on (orbitY − y)           (Wish 3)
add the three wishes               → desiredVel
nudge real velocity toward it      → Lerp                             (smooth ease-in)
```

---

## Two helper bits

- **`_armed` / `OnReleased()`** — wait one physics tick after release so the grab
  system has finished setting the throw velocity before the script reads it.
- **`ResumeOrbit()`** — the shortcut for loaded bubbles: jump straight to `Orbiting`
  (computing the ring from their saved height) instead of re-flying.

---

## Next step for learning

Learn the basics of **vectors**: position vs direction, vector length (magnitude),
unit vectors (normalizing), adding/scaling vectors, and finally the **cross product**
("perpendicular to both inputs"). Once those click, re-read the Orbiting section and
it should all fall into place.

Tip: add a couple of `Debug.DrawRay` calls to draw the radial arrow and the tangent
arrow in the Scene view while a bubble orbits — watching them move makes the
cross-product part click fast.
