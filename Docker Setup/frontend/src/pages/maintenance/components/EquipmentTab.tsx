import { useState, useCallback } from 'react';
import {
  Alert, Badge, Button, Col, Descriptions, Divider, Drawer,
  Form, Input, InputNumber, Modal, Row, Select, Space, Statistic, Table, Tag, Tooltip, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, CheckOutlined, CloseOutlined, ToolOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { equipmentMaintenanceApi } from '../../../api/equipmentMaintenance.api';
import ProgressLogSection from '../../../components/shared/ProgressLogSection';
import {
  MR_STATUS_META, EQUIPMENT_TYPE_META, PRIORITY_META, OFFICE_LOCATIONS,
} from '../../../types';
import type { EquipmentMaintenance, MaintenanceRequestStatus, RequestPriority } from '../../../types';
import { useAuthStore } from '../../../store/authStore';

dayjs.extend(relativeTime);

const { Text } = Typography;
const { TextArea } = Input;

const EQ_TYPES = ['GeneratorService','ACService','UPSMaintenance','PumpService','Electrical','Other'];
const STATUS_TABS = ['','Pending','Approved','Ongoing','AwaitingSpares','AwaitingFunds','Completed','Rejected'];
const STATUS_LABELS: Record<string, string> = {
  '':'All','Pending':'Pending','Approved':'Approved','Ongoing':'Ongoing',
  'AwaitingSpares':'Awaiting Spares','AwaitingFunds':'Awaiting Funds',
  'Completed':'Completed','Rejected':'Rejected',
};

function buildColumns(onView: (r: EquipmentMaintenance) => void): ColumnsType<EquipmentMaintenance> {
  return [
    { title:'Ref #', dataIndex:'requestNumber', key:'ref', width:110,
      render:(v:string)=><Text code style={{fontSize:12}}>{v}</Text> },
    { title:'Asset', dataIndex:'assetDescription', key:'asset', ellipsis:true,
      render:(v:string,r)=>(
        <div>
          <div style={{fontSize:13,fontWeight:500}}>{v}</div>
          <Text type="secondary" style={{fontSize:11}}>{r.assetNo}</Text>
        </div>
      )},
    { title:'Type', dataIndex:'maintenanceType', key:'type', width:150,
      render:(v:string)=>{ const m=EQUIPMENT_TYPE_META[v as keyof typeof EQUIPMENT_TYPE_META]; return <Tag color={m?.color}>{m?.label??v}</Tag>; }},
    { title:'Status', dataIndex:'status', key:'status', width:140,
      render:(v:MaintenanceRequestStatus)=>{ const m=MR_STATUS_META[v]; return <Badge status={m?.badge as any} text={m?.label??v} />; }},
    { title:'Priority', dataIndex:'priority', key:'priority', width:90,
      render:(v:RequestPriority)=>{ const m=PRIORITY_META[v]; return <Tag color={m?.color}>{m?.label??v}</Tag>; }},
    { title:'End User', dataIndex:'endUser', key:'endUser', width:130, ellipsis:true },
    { title:'Location', dataIndex:'location', key:'location', width:140, ellipsis:true },
    { title:'Running Hrs', dataIndex:'runningHours', key:'hours', width:105,
      render:(v?:number)=>v!=null?<Text style={{fontSize:13}}>{v.toLocaleString()}h</Text>:<Text type="secondary">—</Text>},
    { title:'Days', dataIndex:'daysOpen', key:'days', width:70,
      render:(v:number)=><Text type={v>14?'warning':'secondary'} style={{fontSize:12}}>{v}d</Text>},
    { title:'', key:'action', width:65,
      render:(_:unknown,r:EquipmentMaintenance)=><Button size="small" onClick={()=>onView(r)}>View</Button>},
  ];
}

export default function EquipmentTab() {
  const role = useAuthStore(s => s.user?.role);
  const qc   = useQueryClient();

  const [activeStatus, setActiveStatus] = useState('');
  const [search,       setSearch]       = useState('');
  const [page,         setPage]         = useState(1);
  const [selected,     setSelected]     = useState<EquipmentMaintenance|null>(null);
  const [drawerOpen,   setDrawerOpen]   = useState(false);
  const [createOpen,   setCreateOpen]   = useState(false);
  const [rejectOpen,   setRejectOpen]   = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [completeOpen, setCompleteOpen] = useState(false);
  const [workDone,     setWorkDone]     = useState('');
  const [actionedBy,   setActionedBy]   = useState('');
  const [actionLoading,setActionLoading]= useState(false);
  const [actionError,  setActionError]  = useState<string|null>(null);
  const [createForm]   = Form.useForm();
  const [createLoading,setCreateLoading]= useState(false);
  const [createError,  setCreateError]  = useState<string|null>(null);
  const [locationSel,  setLocationSel]  = useState<string|null>(null);

  const isApprover = ['DepartmentManager','Supervisor','SystemAdmin'].includes(role??'');
  const refresh = useCallback(()=>qc.invalidateQueries({queryKey:['eq-maint']}), [qc]);

  const { data, isFetching } = useQuery({
    queryKey: ['eq-maint','list',activeStatus,search,page],
    queryFn: ()=>equipmentMaintenanceApi.list({status:activeStatus||undefined,search:search||undefined,page,pageSize:15}),
  });

  const { data: stats } = useQuery({
    queryKey: ['eq-maint','stats'],
    queryFn: equipmentMaintenanceApi.stats,
    refetchInterval: 30_000,
  });

  const openDetail = (r:EquipmentMaintenance) => { setSelected(r); setDrawerOpen(true); setActionError(null); };

  const act = async (fn:()=>Promise<unknown>) => {
    setActionLoading(true); setActionError(null);
    try { await fn(); refresh(); if(selected) equipmentMaintenanceApi.getById(selected.id).then(setSelected).catch(()=>{}); }
    catch(e:unknown){ setActionError((e as {response?:{data?:{message?:string}}})?.response?.data?.message??'Action failed.'); }
    finally { setActionLoading(false); }
  };

  const handleCreate = async (values: Record<string, unknown>) => {
    setCreateLoading(true); setCreateError(null);
    try {
      const location = values.locationSelect==='Other'?(values.locationOther as string|undefined)?.trim()??'Other':values.locationSelect as string;
      await equipmentMaintenanceApi.create({
        assetNo: (values.assetNo as string).trim(), assetDescription:(values.assetDescription as string).trim(),
        maintenanceType:values.maintenanceType as string, endUser:values.endUser as string,
        location, description:(values.description as string).trim(), priority:values.priority as string,
        runningHours:values.runningHours as number|undefined,
        nextServiceHour:values.nextServiceHour as number|undefined,
      });
      createForm.resetFields(); setLocationSel(null); setCreateOpen(false); refresh();
    } catch(e:unknown){ setCreateError((e as {response?:{data?:{message?:string}}})?.response?.data?.message??'Failed to submit.'); }
    finally { setCreateLoading(false); }
  };

  return (
    <div>
      {/* Stats */}
      <Row gutter={12} style={{marginBottom:20}}>
        {[
          {label:'Pending',         value:stats?.pending,            color:'#fa8c16', key:'Pending'},
          {label:'Approved',        value:stats?.approved,           color:'#1677ff', key:'Approved'},
          {label:'Ongoing',         value:stats?.ongoing,            color:'#722ed1', key:'Ongoing'},
          {label:'Awaiting Spares', value:stats?.awaitingSpares,     color:'#d4a015', key:'AwaitingSpares'},
          {label:'Awaiting Funds',  value:stats?.awaitingFunds,      color:'#fa541c', key:'AwaitingFunds'},
          {label:'Done This Month', value:stats?.completedThisMonth, color:'#52c41a', key:'Completed'},
        ].map(s=>(
          <Col key={s.label} style={{flex:'1 1 130px',minWidth:120,marginBottom:8}}>
            <div onClick={()=>{setActiveStatus(s.key);setPage(1);}}
              style={{background:'#fff',borderRadius:8,padding:'12px 16px',cursor:'pointer',border:'1px solid #f0f0f0',boxShadow:'0 1px 2px rgba(0,0,0,.04)'}}>
              <Statistic title={<Text style={{fontSize:11}}>{s.label}</Text>}
                value={s.value??0} valueStyle={{color:s.color,fontSize:22,fontWeight:700}} />
            </div>
          </Col>
        ))}
      </Row>

      {/* Tabs + search */}
      <div style={{display:'flex',gap:4,flexWrap:'wrap',marginBottom:12}}>
        {STATUS_TABS.map(t=>(
          <Button key={t} size="small" type={activeStatus===t?'primary':'default'}
            onClick={()=>{setActiveStatus(t);setPage(1);}}>
            {STATUS_LABELS[t]}
          </Button>
        ))}
        <div style={{marginLeft:'auto',display:'flex',gap:8}}>
          <Input.Search placeholder="Search asset, description, end user…" style={{width:280}} allowClear
            onSearch={v=>{setSearch(v);setPage(1);}} onChange={e=>!e.target.value&&setSearch('')} />
          <Button type="primary" icon={<PlusOutlined/>} onClick={()=>setCreateOpen(true)}>New Request</Button>
        </div>
      </div>

      <Table<EquipmentMaintenance>
        columns={buildColumns(openDetail)} dataSource={data?.items??[]} rowKey="id" loading={isFetching}
        pagination={{current:page,pageSize:15,total:data?.totalCount??0,onChange:p=>setPage(p),
          showTotal:(t,[f,to])=>`${f}–${to} of ${t}`,showSizeChanger:false}}
        onRow={r=>({onClick:()=>openDetail(r),style:{cursor:'pointer'}})}
        size="middle" scroll={{x:1000}} />

      {/* Create modal */}
      <Modal title={<><ToolOutlined/> New Equipment Maintenance Request</>}
        open={createOpen} onOk={()=>createForm.submit()}
        onCancel={()=>{setCreateOpen(false);createForm.resetFields();setLocationSel(null);setCreateError(null);}}
        okText="Submit" confirmLoading={createLoading} width={560} destroyOnClose>
        {createError&&<Alert message={createError} type="error" showIcon style={{marginBottom:12}}/>}
        <Form form={createForm} layout="vertical" onFinish={handleCreate}>
          <Row gutter={12}>
            <Col span={10}>
              <Form.Item name="assetNo" label="Asset / Tag Number" rules={[{required:true}]}>
                <Input placeholder="e.g. 6660003188"/>
              </Form.Item>
            </Col>
            <Col span={14}>
              <Form.Item name="assetDescription" label="Asset Description" rules={[{required:true}]}>
                <Input placeholder="e.g. DR Cummins 275KVA Generator"/>
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="maintenanceType" label="Maintenance Type" rules={[{required:true}]}>
                <Select placeholder="Select…" options={EQ_TYPES.map(t=>({value:t,label:EQUIPMENT_TYPE_META[t as keyof typeof EQUIPMENT_TYPE_META]?.label??t}))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="priority" label="Priority" initialValue="Normal">
                <Select options={Object.entries(PRIORITY_META).map(([k,m])=>({value:k,label:<Tag color={m.color}>{m.label}</Tag>}))} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="description" label="Description of Fault / Work Required" rules={[{required:true}]}>
            <TextArea rows={3} placeholder="Describe the fault, service required, symptoms…" maxLength={2000} showCount />
          </Form.Item>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="endUser" label="End User / Department" rules={[{required:true}]}>
                <Input placeholder="e.g. DR, PHC Office, Woji"/>
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="locationSelect" label="Location" rules={[{required:true}]}>
                <Select placeholder="Select…" onChange={(v:string)=>setLocationSel(v)}
                  options={OFFICE_LOCATIONS.map(l=>({value:l,label:l}))} />
              </Form.Item>
            </Col>
          </Row>
          {locationSel==='Other'&&(
            <Form.Item name="locationOther" label="Specify Location" rules={[{required:true}]}>
              <Input placeholder="Enter specific location…"/>
            </Form.Item>
          )}
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="runningHours" label="Current Running Hours">
                <InputNumber style={{width:'100%'}} placeholder="e.g. 13602" min={0}/>
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="nextServiceHour" label="Next Service Due At">
                <InputNumber style={{width:'100%'}} placeholder="e.g. 13852" min={0}/>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>

      {/* Detail drawer */}
      {selected&&(
        <Drawer title={<Space><Text code>{selected.requestNumber}</Text><span style={{fontWeight:600}}>{selected.assetDescription}</span></Space>}
          open={drawerOpen} onClose={()=>setDrawerOpen(false)} width={520}
          extra={<Space wrap>
            {isApprover&&selected.status==='Pending'&&<>
              <Button type="primary" size="small" icon={<CheckOutlined/>} loading={actionLoading}
                onClick={()=>act(()=>equipmentMaintenanceApi.approve(selected.id))}>Approve</Button>
              <Button danger size="small" icon={<CloseOutlined/>} onClick={()=>setRejectOpen(true)}>Reject</Button>
            </>}
            {isApprover&&selected.status==='Approved'&&
              <Button type="primary" size="small" onClick={()=>act(()=>equipmentMaintenanceApi.updateStatus(selected.id,'Ongoing'))}>Mark Ongoing</Button>}
            {isApprover&&selected.status==='Ongoing'&&<>
              <Button size="small" onClick={()=>act(()=>equipmentMaintenanceApi.updateStatus(selected.id,'AwaitingSpares'))}>Awaiting Spares</Button>
              <Button size="small" onClick={()=>act(()=>equipmentMaintenanceApi.updateStatus(selected.id,'AwaitingFunds'))}>Awaiting Funds</Button>
              <Button type="primary" size="small" icon={<CheckOutlined/>} onClick={()=>setCompleteOpen(true)}>Complete</Button>
            </>}
            {isApprover&&(selected.status==='AwaitingSpares'||selected.status==='AwaitingFunds')&&
              <Button type="primary" size="small" icon={<CheckOutlined/>} onClick={()=>setCompleteOpen(true)}>Complete</Button>}
          </Space>}>
          {actionError&&<Alert message={actionError} type="error" showIcon closable onClose={()=>setActionError(null)} style={{marginBottom:12}}/>}
          <Space wrap style={{marginBottom:14}}>
            {(()=>{const m=MR_STATUS_META[selected.status];return <Badge status={m?.badge as any} text={<Text strong>{m?.label}</Text>}/>;})()}
            {(()=>{const m=EQUIPMENT_TYPE_META[selected.maintenanceType as keyof typeof EQUIPMENT_TYPE_META];return <Tag color={m?.color}>{m?.label??selected.maintenanceType}</Tag>;})()}
            {(()=>{const m=PRIORITY_META[selected.priority];return <Tag color={m?.color}>{m?.label} Priority</Tag>;})()}
          </Space>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Ref #">{selected.requestNumber}</Descriptions.Item>
            <Descriptions.Item label="Asset No.">{selected.assetNo}</Descriptions.Item>
            <Descriptions.Item label="Asset">{selected.assetDescription}</Descriptions.Item>
            <Descriptions.Item label="Description"><Text style={{whiteSpace:'pre-wrap'}}>{selected.description}</Text></Descriptions.Item>
            <Descriptions.Item label="End User">{selected.endUser}</Descriptions.Item>
            <Descriptions.Item label="Location">{selected.location}</Descriptions.Item>
            {selected.runningHours!=null&&<Descriptions.Item label="Running Hours">{selected.runningHours.toLocaleString()} h</Descriptions.Item>}
            {selected.nextServiceHour!=null&&<Descriptions.Item label="Next Service At">{selected.nextServiceHour.toLocaleString()} h</Descriptions.Item>}
            <Descriptions.Item label="Raised By">{selected.requestedByName} <Text type="secondary">({selected.requestedByEmail})</Text></Descriptions.Item>
            <Descriptions.Item label="Submitted"><Tooltip title={dayjs(selected.createdAt).format('D MMM YYYY, HH:mm')}>{dayjs(selected.createdAt).fromNow()} ({selected.daysOpen}d ago)</Tooltip></Descriptions.Item>
          </Descriptions>
          {selected.approvedByName&&(<>
            <Divider orientation="left" orientationMargin={0} style={{fontSize:12}}>{selected.status==='Rejected'?'Rejection':'Approval'}</Divider>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label={selected.status==='Rejected'?'Rejected By':'Approved By'}>{selected.approvedByName}</Descriptions.Item>
              {selected.approvedAt&&<Descriptions.Item label="Date">{dayjs(selected.approvedAt).format('D MMM YYYY, HH:mm')}</Descriptions.Item>}
              {selected.rejectionReason&&<Descriptions.Item label="Reason"><Text type="danger">{selected.rejectionReason}</Text></Descriptions.Item>}
            </Descriptions>
          </>)}
          {selected.workDone&&(<>
            <Divider orientation="left" orientationMargin={0} style={{fontSize:12}}>Work Done</Divider>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Work Done"><Text style={{whiteSpace:'pre-wrap'}}>{selected.workDone}</Text></Descriptions.Item>
              {selected.actionedBy&&<Descriptions.Item label="Actioned By">{selected.actionedBy}</Descriptions.Item>}
              {selected.completedAt&&<Descriptions.Item label="Completed">{dayjs(selected.completedAt).format('D MMM YYYY')}</Descriptions.Item>}
            </Descriptions>
          </>)}

          <ProgressLogSection
            module="Equipment"
            entityId={selected.id}
            refNumber={selected.requestNumber}
            taskTitle={selected.assetDescription}
          />
        </Drawer>
      )}

      {/* Reject modal */}
      <Modal title="Reject Request" open={rejectOpen}
        onOk={async()=>{if(!rejectReason.trim()||!selected)return;await act(()=>equipmentMaintenanceApi.reject(selected.id,rejectReason));setRejectOpen(false);setRejectReason('');}}
        onCancel={()=>{setRejectOpen(false);setRejectReason('');}}
        okText="Confirm Rejection" okButtonProps={{danger:true,disabled:!rejectReason.trim()}} confirmLoading={actionLoading}>
        <p>Reason for rejecting <strong>{selected?.requestNumber}</strong>:</p>
        <TextArea rows={3} value={rejectReason} onChange={e=>setRejectReason(e.target.value)} maxLength={1000} showCount/>
      </Modal>

      {/* Complete modal */}
      <Modal title="Mark as Completed" open={completeOpen}
        onOk={async()=>{if(!workDone.trim()||!selected)return;await act(()=>equipmentMaintenanceApi.complete(selected.id,workDone,actionedBy||undefined));setCompleteOpen(false);setWorkDone('');setActionedBy('');}}
        onCancel={()=>{setCompleteOpen(false);setWorkDone('');setActionedBy('');}}
        okText="Confirm Completion" okButtonProps={{disabled:!workDone.trim()}} confirmLoading={actionLoading} width={480}>
        <Space direction="vertical" style={{width:'100%'}} size={12}>
          <div>
            <Text strong style={{display:'block',marginBottom:6}}>Description of Work Done *</Text>
            <TextArea rows={4} value={workDone} onChange={e=>setWorkDone(e.target.value)} placeholder="Describe work performed, parts replaced, quantities…" maxLength={2000}/>
          </div>
          <div>
            <Text strong style={{display:'block',marginBottom:6}}>Actioned By (optional)</Text>
            <Input value={actionedBy} onChange={e=>setActionedBy(e.target.value)} placeholder="e.g. Woji Store, Third Party, staff name"/>
          </div>
        </Space>
      </Modal>
    </div>
  );
}
