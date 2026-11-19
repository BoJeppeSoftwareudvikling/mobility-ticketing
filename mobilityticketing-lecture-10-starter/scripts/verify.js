// Your implementation. This function receives only successful query results.
// actual is a document or null; expected contains the four fields below.
// Return exactly: PresentMatches, PresentConflicts, or AbsentAtVerification.
// Compare _id, tripId, status and sourceVersion. Do not query or write here.
function compareChange(actual, expected) {
  if (actual === null) return 'AbsentAtVerification';

  if (actual._id !== expected._id ||
      actual.tripId !== expected.tripId ||
      actual.status !== expected.status ||
      actual.sourceVersion !== expected.sourceVersion) {
    return 'PresentConflicts';
  }

  return 'PresentMatches';
}
