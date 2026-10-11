namespace CivicConnect.Tests
{
    public static class LoadTest
    {
        public const string Script = @"
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 20 }, // Ramp-up to 20 virtual users
    { duration: '1m', target: 50 }, // Sustained load at 50 VUs
    { duration: '30s', target: 0 }, // Ramp-down
  ],
  thresholds: {
    http_req_duration: ['p(95)<200'], // 95% of requests must complete below 200ms
    http_req_failed: ['rate<0.01'], // Error rate must be < 1%
  },
};

export default function () {
  const res = http.get('http://localhost:5000/api/requests/1');
  check(res, {
    'status is 200 or 404': (r) => r.status === 200 || r.status === 404,
    'response time < 200ms': (r) => r.timings.duration < 200,
  });
  sleep(1);
}
";
    }
}