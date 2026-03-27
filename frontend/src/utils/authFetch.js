const TOKEN_KEY = 'mbp_token'; // correct localStorage key

export const getToken = () => localStorage.getItem(TOKEN_KEY);

export const authFetch = async (url, options = {}) => {
  const token = getToken();
  return fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      ...options.headers, // caller headers override defaults
    },
  });
};
