export function settleAdminRequests(requests) {
  return Promise.allSettled(requests);
}

export async function runAdminMutation({ mutate, onSaved, refresh }) {
  const result = await mutate();
  onSaved(result);

  return {
    result,
    refreshSucceeded: await refresh(),
  };
}
