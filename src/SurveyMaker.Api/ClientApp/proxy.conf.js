const PROXY_CONFIG = [
  {
    context: ['/api', '/signin-google'],
    proxyTimeout: 10000,
    target: 'http://localhost:5065',
    secure: false,
    headers: {
      Connection: 'Keep-Alive'
    }
  }
];

module.exports = PROXY_CONFIG;
