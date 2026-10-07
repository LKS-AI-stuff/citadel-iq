import { useState } from 'react';
import { Navigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Divider,
  IconButton,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  alpha,
  useTheme,
} from '@mui/material';
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined';
import HowToRegOutlinedIcon from '@mui/icons-material/HowToRegOutlined';
import KeyOutlinedIcon from '@mui/icons-material/KeyOutlined';
import PersonRemoveOutlinedIcon from '@mui/icons-material/PersonRemoveOutlined';
import ContentCopyOutlinedIcon from '@mui/icons-material/ContentCopyOutlined';
import { GlassSurface } from '../components/common/GlassSurface';
import { SectionHeader } from '../components/common/SectionHeader';
import { ConfirmDialog } from '../components/common/ConfirmDialog';
import { useToast } from '../components/common/ToastProvider';
import { useMembers } from '../hooks/useMembers';
import { useJoinRequests } from '../hooks/useJoinRequests';
import { useJoinCode } from '../hooks/useJoinCode';
import { useSession } from '../session/useSession';
import { actionIconButtonSx, folderAccentColor } from '../theme/glass';
import { ROLE_RANK, type WorkspaceRole } from '../types/session';
import type { MemberDto } from '../types/organization';

const ROLES: WorkspaceRole[] = ['Member', 'Admin', 'Owner'];
const LAST_OWNER_HINT = 'An organization needs at least one Owner.';

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

/** Organization administration: join requests, members and roles, and the join code. Admin and Owner only —
 * anyone else is redirected before any admin data is requested. */
export function AdminPage() {
  const { session, can } = useSession();
  return can.administer && session?.role ? <AdminPageContent /> : <Navigate to="/" replace />;
}

function AdminPageContent() {
  const { session, refresh } = useSession();
  const theme = useTheme();
  const accent = folderAccentColor(theme);
  const { showToast } = useToast();
  const { members, error: membersError, refetch: refetchMembers, changeRole, removeMember } = useMembers();
  const { requests, error: requestsError, approve, reject } = useJoinRequests(refetchMembers);
  const { code, error: codeError, isRegenerating, regenerate } = useJoinCode();
  const [approveRoles, setApproveRoles] = useState<Record<string, WorkspaceRole>>({});
  const [memberToRemove, setMemberToRemove] = useState<MemberDto | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);
  const [isConfirmRegenerateOpen, setConfirmRegenerateOpen] = useState(false);

  const actorRole = session!.role!;
  const isOwner = actorRole === 'Owner';
  const ownerCount = members?.filter((m) => m.role === 'Owner').length ?? 0;
  // Mirrors the server's rules so the UI only offers allowed changes; the server enforces them regardless.
  const canManage = (member: MemberDto) => isOwner || member.role !== 'Owner';
  const isLastOwner = (member: MemberDto) => member.role === 'Owner' && ownerCount <= 1;
  const assignableRoles = ROLES.filter((r) => isOwner || ROLE_RANK[r] < ROLE_RANK.Owner);

  const withToast = async (action: () => Promise<void>, success: string) => {
    try {
      await action();
      showToast(success, 'success');
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Something went wrong.', 'error');
    }
  };

  const handleRoleChange = (member: MemberDto, role: WorkspaceRole) =>
    withToast(async () => {
      await changeRole(member.userId, role);
      // Changing your own role changes what this page (and the app) may show.
      if (member.isCurrentUser) await refresh();
    }, `${member.displayName} is now ${role === 'Admin' ? 'an' : 'a'} ${role}.`);

  const handleRemove = async () => {
    if (!memberToRemove) return;
    setIsRemoving(true);
    await withToast(async () => {
      await removeMember(memberToRemove.userId);
      if (memberToRemove.isCurrentUser) await refresh();
    }, `${memberToRemove.displayName} was removed.`);
    setIsRemoving(false);
    setMemberToRemove(null);
  };

  const copyCode = async () => {
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      showToast('Join code copied.', 'success');
    } catch {
      showToast('Copy failed — select the code and copy it manually.', 'error');
    }
  };

  const divider = <Divider sx={{ my: { xs: 2.5, sm: 3 }, borderColor: (t) => alpha(t.palette.text.primary, 0.1) }} />;

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 800 }}>
          {session!.workspace?.name}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Organization administration
        </Typography>
      </Box>

      <GlassSurface component="section" sx={{ p: { xs: 2, sm: 3 } }}>
        <Box component="section" aria-label="Join requests">
          <SectionHeader
            icon={<HowToRegOutlinedIcon sx={{ color: accent, fontSize: 20 }} />}
            color={accent}
            title="Join requests"
            count={requests?.length ?? 0}
          />
          {requestsError && <Alert severity="error">{requestsError}</Alert>}
          {requests?.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              No pending requests. Share the join code below with people who should join.
            </Typography>
          )}
          <Stack spacing={1.25}>
            {requests?.map((request) => (
              <GlassSurface key={request.id} radius={14} sx={{ p: 1.5, display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
                <Box sx={{ flex: 1, minWidth: 180 }}>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>
                    {request.displayName}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {request.email} · requested {formatDate(request.createdAtUtc)}
                  </Typography>
                </Box>
                <Select
                  size="small"
                  value={approveRoles[request.id] ?? 'Member'}
                  onChange={(e) => setApproveRoles((prev) => ({ ...prev, [request.id]: e.target.value as WorkspaceRole }))}
                  inputProps={{ 'aria-label': `Role for ${request.displayName}` }}
                >
                  {assignableRoles.map((role) => (
                    <MenuItem key={role} value={role}>
                      {role}
                    </MenuItem>
                  ))}
                </Select>
                <Button
                  variant="contained"
                  size="small"
                  onClick={() => withToast(() => approve(request.id, approveRoles[request.id] ?? 'Member'), `${request.displayName} joined.`)}
                >
                  Approve
                </Button>
                <Button
                  variant="outlined"
                  color="error"
                  size="small"
                  onClick={() => withToast(() => reject(request.id), `${request.displayName}'s request was declined.`)}
                >
                  Reject
                </Button>
              </GlassSurface>
            ))}
          </Stack>
        </Box>

        {divider}

        <Box component="section" aria-label="Members">
          <SectionHeader
            icon={<GroupOutlinedIcon sx={{ color: accent, fontSize: 20 }} />}
            color={accent}
            title="Members"
            count={members?.length ?? 0}
          />
          {membersError && <Alert severity="error">{membersError}</Alert>}
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small" aria-label="Members">
              <TableHead>
                <TableRow>
                  <TableCell>Name</TableCell>
                  <TableCell>Email</TableCell>
                  <TableCell>Role</TableCell>
                  <TableCell>Joined</TableCell>
                  <TableCell align="right">
                    <Box component="span" sx={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0 0 0 0)' }}>
                      Actions
                    </Box>
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {members?.map((member) => {
                  const locked = !canManage(member) || isLastOwner(member);
                  const lockReason = isLastOwner(member) ? LAST_OWNER_HINT : 'Only an Owner can change an Owner.';
                  return (
                    <TableRow key={member.userId}>
                      <TableCell sx={{ fontWeight: 600 }}>
                        {member.displayName}
                        {member.isCurrentUser ? ' (you)' : ''}
                      </TableCell>
                      <TableCell>{member.email}</TableCell>
                      <TableCell>
                        <Tooltip title={locked ? lockReason : ''}>
                          <span>
                            <Select
                              size="small"
                              value={member.role}
                              disabled={locked}
                              onChange={(e) => handleRoleChange(member, e.target.value as WorkspaceRole)}
                              inputProps={{ 'aria-label': `Role of ${member.displayName}` }}
                            >
                              {ROLES.filter((r) => r === member.role || assignableRoles.includes(r)).map((role) => (
                                <MenuItem key={role} value={role}>
                                  {role}
                                </MenuItem>
                              ))}
                            </Select>
                          </span>
                        </Tooltip>
                      </TableCell>
                      <TableCell>{formatDate(member.joinedAtUtc)}</TableCell>
                      <TableCell align="right">
                        <Tooltip title={locked ? lockReason : 'Remove from organization'}>
                          <span>
                            <IconButton
                              size="small"
                              disabled={locked}
                              onClick={() => setMemberToRemove(member)}
                              aria-label={`Remove ${member.displayName}`}
                              sx={actionIconButtonSx(theme.palette.error.main)}
                            >
                              <PersonRemoveOutlinedIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </Box>
        </Box>

        {divider}

        <Box component="section" aria-labelledby="join-code-heading">
          <Stack direction="row" sx={{ alignItems: 'center', gap: 1.5, mb: 2 }}>
            <KeyOutlinedIcon sx={{ color: accent }} />
            <Typography variant="h5" id="join-code-heading">
              Join code
            </Typography>
          </Stack>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            People enter this code when they sign up. You still approve every request. Regenerating stops the old code
            from working.
          </Typography>
          {codeError && <Alert severity="error">{codeError}</Alert>}
          <Stack direction="row" sx={{ alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <Typography
              component="code"
              sx={{ fontFamily: 'monospace', fontSize: 22, fontWeight: 700, letterSpacing: '0.12em', userSelect: 'all' }}
            >
              {code ?? '…'}
            </Typography>
            <Tooltip title="Copy">
              <IconButton onClick={copyCode} aria-label="Copy join code" sx={actionIconButtonSx(accent)} disabled={!code}>
                <ContentCopyOutlinedIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <Button variant="outlined" onClick={() => setConfirmRegenerateOpen(true)} disabled={isRegenerating}>
              Regenerate
            </Button>
          </Stack>
        </Box>
      </GlassSurface>

      <ConfirmDialog
        open={memberToRemove !== null}
        title={memberToRemove?.isCurrentUser ? 'Remove yourself?' : 'Remove this member?'}
        description={
          <Typography variant="body2" color="text.secondary">
            {memberToRemove?.displayName}'s account will be closed permanently. Documents they uploaded stay with the
            organization. To come back they would sign up again with a different email.
          </Typography>
        }
        confirmLabel="Remove"
        isConfirming={isRemoving}
        onConfirm={handleRemove}
        onClose={() => setMemberToRemove(null)}
      />

      <ConfirmDialog
        open={isConfirmRegenerateOpen}
        title="Regenerate the join code?"
        description={
          <Typography variant="body2" color="text.secondary">
            The current code stops working immediately. Requests already sent stay pending.
          </Typography>
        }
        confirmLabel="Regenerate"
        isConfirming={isRegenerating}
        onConfirm={async () => {
          await withToast(regenerate, 'A new join code was created.');
          setConfirmRegenerateOpen(false);
        }}
        onClose={() => setConfirmRegenerateOpen(false)}
      />
    </Stack>
  );
}
