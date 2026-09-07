import courtIndoor from "../assets/images/court-indoor.jpg";
import courtNight from "../assets/images/court-night.jpg";
import courtSunset from "../assets/images/court-sunset.jpg";

export const courtImages = [courtIndoor, courtSunset, courtNight];

export function getCourtImage(courtId, fallbackIndex = 0) {
  const numericId = Number(courtId);
  const index = Number.isFinite(numericId) && numericId > 0
    ? numericId - 1
    : fallbackIndex;

  return courtImages[Math.abs(index) % courtImages.length];
}
