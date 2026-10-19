const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const workflow = fs.readFileSync(
  path.join(__dirname, '..', 'workflows', 'workflow.yml'),
  'utf8'
);
const scriptMatch = workflow.match(
  /          script: \|\r?\n((?:            [^\r\n]*(?:\r?\n|$))*)/
);
assert.ok(scriptMatch, 'The release policy script must exist in the workflow.');
const script = scriptMatch[1].replace(/^ {12}/gm, '');

test('no workflow uses pull_request_target and the labeler is removed', () => {
  const workflowDirectory = path.join(__dirname, '..', 'workflows');
  for (const file of fs.readdirSync(workflowDirectory)) {
    if (/\.ya?ml$/i.test(file)) {
      const content = fs.readFileSync(path.join(workflowDirectory, file), 'utf8');
      assert.doesNotMatch(content, /\bpull_request_target\b/, file);
    }
  }
  assert.equal(fs.existsSync(path.join(workflowDirectory, 'labeler.yml')), false);
  assert.equal(fs.existsSync(path.join(__dirname, '..', 'labeler.yml')), false);
});

function mergedPullRequest(overrides = {}) {
  return {
    merged_at: '2026-10-19T12:00:00Z',
    merge_commit_sha: 'trusted-commit',
    base: { repo: { id: 123 }, ref: 'main' },
    labels: [{ name: 'Release' }],
    ...overrides
  };
}

async function evaluate(pullRequests, { branch = 'main', apiError } = {}) {
  const outputs = {};
  const endpoint = Symbol('listPullRequestsAssociatedWithCommit');
  const context = {
    repo: { owner: 'microsoft', repo: 'CollectServiceFabricData' },
    sha: 'trusted-commit',
    ref: `refs/heads/${branch}`,
    payload: { repository: { id: 123 } }
  };
  const github = {
    rest: { repos: { listPullRequestsAssociatedWithCommit: endpoint } },
    paginate: async (method, options) => {
      assert.equal(method, endpoint);
      assert.deepEqual({ ...options }, {
        ...context.repo,
        commit_sha: context.sha,
        per_page: 100
      });
      if (apiError) {
        throw apiError;
      }
      return pullRequests;
    }
  };
  await vm.runInNewContext(`(async () => {\n${script}\n})()`, {
    github,
    context,
    core: {
      setOutput: (name, value) => {
        outputs[name] = value;
      }
    }
  });
  return outputs;
}

test('a Release-labelled merge publishes a stable release', async () => {
  assert.deepEqual(await evaluate([mergedPullRequest()]), {
    publish: true,
    prerelease: false
  });
});

test('a Pre-release-labelled merge publishes a prerelease', async () => {
  const pr = mergedPullRequest({ labels: [{ name: 'Pre-release' }] });
  assert.deepEqual(await evaluate([pr]), { publish: true, prerelease: true });
});

test('Pre-release takes precedence when both labels are present', async () => {
  const pr = mergedPullRequest({
    labels: [{ name: 'Release' }, { name: 'Pre-release' }]
  });
  assert.deepEqual(await evaluate([pr]), { publish: true, prerelease: true });
});

test('a fork PR can release only after its merge matches the trusted push', async () => {
  const pr = mergedPullRequest({
    head: { repo: { id: 456 }, sha: 'untrusted-head' }
  });
  assert.equal((await evaluate([pr])).publish, true);
  pr.merge_commit_sha = 'untrusted-head';
  assert.equal((await evaluate([pr])).publish, false);
});

test('master merges are supported', async () => {
  const pr = mergedPullRequest({ base: { repo: { id: 123 }, ref: 'master' } });
  assert.equal((await evaluate([pr], { branch: 'master' })).publish, true);
});

for (const [name, pullRequests] of [
  ['direct push', []],
  ['unlabelled merge', [mergedPullRequest({ labels: [] })]],
  ['unrelated label', [mergedPullRequest({ labels: [{ name: 'documentation' }] })]],
  ['case-mismatched release label', [mergedPullRequest({ labels: [{ name: 'release' }] })]],
  ['open or unmerged PR', [mergedPullRequest({ merged_at: null })]],
  ['different merge commit', [mergedPullRequest({ merge_commit_sha: 'older-commit' })]],
  ['different base repository', [mergedPullRequest({ base: { repo: { id: 456 }, ref: 'main' } })]],
  ['different base branch', [mergedPullRequest({ base: { repo: { id: 123 }, ref: 'master' } })]]
]) {
  test(`${name} does not publish`, async () => {
    assert.deepEqual(await evaluate(pullRequests), {
      publish: false,
      prerelease: false
    });
  });
}

test('only the merged PR matching the pushed tip determines labels', async () => {
  const older = mergedPullRequest({ merge_commit_sha: 'older-commit' });
  const tip = mergedPullRequest({ labels: [] });
  assert.equal((await evaluate([older, tip])).publish, false);
});

test('ambiguous merged PR matches fail explicitly', async () => {
  await assert.rejects(
    evaluate([mergedPullRequest(), mergedPullRequest()]),
    /Multiple merged pull requests/
  );
});

test('GitHub API failures fail explicitly', async () => {
  await assert.rejects(
    evaluate([], { apiError: new Error('GitHub API unavailable') }),
    /GitHub API unavailable/
  );
});
